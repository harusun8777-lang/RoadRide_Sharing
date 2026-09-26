locals {
  tags = {
    service    = "rideshare"
    managed_by = "terraform"
  }

  # tfvars で指定されていればその値を、なければ Terraform が生成した値を使う
  sql_admin_password = coalesce(var.sql_admin_password, random_password.sql_admin.result)
  jwt_signing_key    = coalesce(var.jwt_signing_key, random_password.jwt_signing_key.result)
}

# ACR や SQL Server のように Azure 全体で一意な名前が必要なリソース用のサフィックス
resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

# ---------------------------------------------------------------------------
# 監視
# ---------------------------------------------------------------------------

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-rideshare"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# ---------------------------------------------------------------------------
# コンテナレジストリ
# ---------------------------------------------------------------------------

resource "azurerm_container_registry" "main" {
  name                = "crrideshare${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

# Container App が ACR からイメージを取得するためのマネージドID
resource "azurerm_user_assigned_identity" "backend" {
  name                = "id-rideshare-backend"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

resource "azurerm_role_assignment" "backend_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.backend.principal_id
}

# ---------------------------------------------------------------------------
# データベース
# ---------------------------------------------------------------------------

resource "random_password" "sql_admin" {
  length           = 32
  special          = true
  override_special = "!#%*-_=+"
}

resource "azurerm_mssql_server" "main" {
  name                         = "sql-rideshare-${random_string.suffix.result}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = local.sql_admin_password
  minimum_tls_version          = "1.2"
  tags                         = local.tags
}

# DTU モデルの Basic（5 DTU）。Terraform で指定できる中で一番安い
resource "azurerm_mssql_database" "main" {
  name        = "RoadRideSharing"
  server_id   = azurerm_mssql_server.main.id
  sku_name    = "Basic"
  max_size_gb = 2
  tags        = local.tags
}

# Container Apps（Azure 内部）からの接続を許可する
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# ---------------------------------------------------------------------------
# バックエンド（Container Apps）
# ---------------------------------------------------------------------------

resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

# ワークロードプロファイルを指定しないので、従量課金（Consumption）のみのサーバーレス環境になる
resource "azurerm_container_app_environment" "main" {
  name                       = "cae-rideshare"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_container_app" "backend" {
  name                         = "ca-rideshare-backend"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.backend.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.backend.id
  }

  secret {
    name  = "db-connection-string"
    value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.main.name};User Id=${var.sql_admin_login};Password=${local.sql_admin_password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30"
  }

  secret {
    name  = "jwt-signing-key"
    value = local.jwt_signing_key
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }

    # Cloudflare に置くフロントエンドからの呼び出しを許可する
    dynamic "cors" {
      for_each = length(var.frontend_origins) > 0 ? [1] : []
      content {
        allowed_origins    = var.frontend_origins
        allowed_methods    = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]
        allowed_headers    = ["Authorization", "Content-Type"]
        max_age_in_seconds = 3600
      }
    }
  }

  template {
    # アクセスがない間は 0 台まで縮退してコストを抑える
    min_replicas = 0
    max_replicas = 2

    container {
      name   = "backend"
      image  = "${azurerm_container_registry.main.login_server}/rideshare-backend:${var.backend_image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name        = "ConnectionStrings__DefaultConnection"
        secret_name = "db-connection-string"
      }

      env {
        name        = "Jwt__SigningKey"
        secret_name = "jwt-signing-key"
      }
    }
  }

  # AcrPull が付与される前にイメージを取得しにいかないようにする
  depends_on = [azurerm_role_assignment.backend_acr_pull]

  # イメージは GitHub Actions（.github/workflows/deploy-backend.yml）が main へのマージごとに更新する。
  # Terraform が古いタグに戻さないよう、作成後のイメージの変更は無視する
  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}

# ---------------------------------------------------------------------------
# GitHub Actions からのデプロイ
# ---------------------------------------------------------------------------

# GitHub Actions が OIDC でログインするためのマネージドID（パスワードやシークレットは不要）
resource "azurerm_user_assigned_identity" "github_actions" {
  name                = "id-rideshare-github-actions"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

# main ブランチで動くワークフローだけがログインできる
resource "azurerm_federated_identity_credential" "github_actions_main" {
  name                = "github-main"
  resource_group_name = azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.github_actions.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = "https://token.actions.githubusercontent.com"
  subject             = "repo:${var.github_repository}:ref:refs/heads/main"
}

# イメージの push
resource "azurerm_role_assignment" "github_actions_acr_push" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPush"
  principal_id         = azurerm_user_assigned_identity.github_actions.principal_id
}

# Container App のイメージの更新（この Container App だけに限定）
resource "azurerm_role_assignment" "github_actions_container_app" {
  scope                = azurerm_container_app.backend.id
  role_definition_name = "Contributor"
  principal_id         = azurerm_user_assigned_identity.github_actions.principal_id
}
