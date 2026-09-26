terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # state は Gitrain など他プロジェクトと分けるため、専用のストレージに置く。
  # 接続先は backend.hcl で渡す（terraform init -backend-config=backend.hcl）
  backend "azurerm" {}
}

provider "azurerm" {
  features {}

  # az CLI の既定サブスクリプションに関係なく、常に YasuiSoftwere にデプロイする
  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id

  # リソースプロバイダーの登録はサブスクリプション全体に影響するため、Terraform からは行わない
  resource_provider_registrations = "none"
}
