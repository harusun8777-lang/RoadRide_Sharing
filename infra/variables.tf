variable "subscription_id" {
  description = "デプロイ先のサブスクリプションID（YasuiSoftwere）"
  type        = string
  default     = "303b6594-5afc-4aae-be3b-feb4e1d454a4"
}

variable "tenant_id" {
  description = "YasuiSoftwere が属するテナントID（az CLI の既定テナントとは異なる）"
  type        = string
  default     = "a45b5a16-0d27-4285-8e28-2d5be8568d98"
}

variable "resource_group_name" {
  description = "このプロジェクトのリソースをまとめるリソースグループ名"
  type        = string
  default     = "rideshare-service"
}

variable "location" {
  description = "リソースを配置するリージョン"
  type        = string
  default     = "japaneast"
}

variable "backend_image_tag" {
  description = "ACR にプッシュした backend イメージのタグ"
  type        = string
  default     = "latest"
}

variable "frontend_origins" {
  description = "API の呼び出しを許可するフロントエンド（Cloudflare）のオリジン。空なら CORS を設定しない"
  type        = list(string)
  default     = []
}

variable "sql_admin_login" {
  description = "Azure SQL の管理者ユーザー名"
  type        = string
  default     = "roadrideadmin"
}
