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

variable "github_repository" {
  # このリポジトリの OIDC トークンの sub は、オーナーとリポジトリの数値IDを含む形式
  # （repo:owner@オーナーID/name@リポジトリID:ref:...）になっている。
  # 実際の値は Azure のエラー AADSTS700213 の "assertion subject" で確認できる
  description = "OIDC トークンの sub に入るリポジトリの表記（owner@オーナーID/name@リポジトリID）"
  type        = string
  default     = "harusun8777-lang@254899395/RoadRide_Sharing@1377844180"
}

variable "backend_image_tag" {
  description = "Container App 作成時の backend イメージのタグ（作成後は GitHub Actions が更新する）"
  type        = string
  default     = "latest"
}

variable "frontend_origins" {
  description = "API の呼び出しを許可するフロントエンド（Cloudflare）のオリジン。空なら CORS を設定しない"
  type        = list(string)
  default     = []
}

variable "sql_admin_password" {
  description = "Azure SQL の管理者パスワード。未指定なら Terraform がランダムに生成する（secrets.auto.tfvars で渡す）"
  type        = string
  default     = null
  sensitive   = true

  validation {
    condition     = var.sql_admin_password == null || try(length(var.sql_admin_password) >= 8 && length(var.sql_admin_password) <= 128, false)
    error_message = "sql_admin_password は 8〜128 文字にしてください。"
  }
}

variable "jwt_signing_key" {
  description = "JWT の署名鍵。未指定なら Terraform がランダムに生成する（secrets.auto.tfvars で渡す）"
  type        = string
  default     = null
  sensitive   = true

  validation {
    # HS256 の鍵は 256bit 以上が必要（backend の JwtOptions.MinSigningKeyBytes と同じ）
    condition     = var.jwt_signing_key == null || try(length(var.jwt_signing_key) >= 32, false)
    error_message = "jwt_signing_key は 32 文字以上にしてください。"
  }
}

variable "sql_admin_login" {
  description = "Azure SQL の管理者ユーザー名"
  type        = string
  default     = "roadrideadmin"
}
