output "backend_url" {
  description = "バックエンドAPIのURL"
  value       = "https://${azurerm_container_app.backend.ingress[0].fqdn}"
}

output "acr_name" {
  description = "イメージのビルド・プッシュ先の ACR 名"
  value       = azurerm_container_registry.main.name
}

output "acr_login_server" {
  description = "ACR のログインサーバー"
  value       = azurerm_container_registry.main.login_server
}

output "sql_server_fqdn" {
  description = "Azure SQL Server の FQDN"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "github_actions_client_id" {
  description = "GitHub Actions の azure/login に渡すマネージドIDのクライアントID"
  value       = azurerm_user_assigned_identity.github_actions.client_id
}
