#!/usr/bin/env bash
# ルートの .env から、Terraform に渡すシークレットを infra/secrets.auto.tfvars に書き出す。
# 使い方: infra ディレクトリで ./scripts/env-to-tfvars.sh
#
# - .env.example と同じ値（リポジトリで公開されているサンプル値）は本番に使わせないため書き出さない。
#   書き出さなかった項目は Terraform が生成したランダムな値のままになる
# - secrets.auto.tfvars は .gitignore 済み。コミットしないこと
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
infra_dir="$(dirname "$script_dir")"
root_dir="$(dirname "$infra_dir")"
env_file="$root_dir/.env"
example_file="$root_dir/.env.example"
out_file="$infra_dir/secrets.auto.tfvars"

# .env のキー => Terraform の変数名
mappings=(
  "MSSQL_SA_PASSWORD=sql_admin_password"
  "JWT_SIGNING_KEY=jwt_signing_key"
)

if [[ ! -f "$env_file" ]]; then
  echo "エラー: $env_file がありません" >&2
  exit 1
fi

# KEY=VALUE 形式のファイルから値を読む（コメント行は無視。前後の引用符は外す）
read_value() {
  local file="$1" key="$2" line value
  [[ -f "$file" ]] || return 0
  line="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n 1 || true)"
  [[ -n "$line" ]] || return 0
  value="${line#*=}"
  value="${value%$'\r'}"
  if [[ "$value" =~ ^\"(.*)\"$ || "$value" =~ ^\'(.*)\'$ ]]; then
    value="${BASH_REMATCH[1]}"
  fi
  printf '%s' "$value"
}

# HCL の文字列リテラル用にエスケープする（\ と " と、テンプレート記法の ${ %{）
escape_hcl() {
  local v="$1"
  v="${v//\\/\\\\}"
  v="${v//\"/\\\"}"
  v="${v//\$\{/\$\$\{}"
  v="${v//%\{/%%\{}"
  printf '%s' "$v"
}

lines=()
for mapping in "${mappings[@]}"; do
  env_key="${mapping%%=*}"
  tf_var="${mapping#*=}"
  value="$(read_value "$env_file" "$env_key")"
  example="$(read_value "$example_file" "$env_key")"

  if [[ -z "$value" ]]; then
    echo "スキップ: $env_key が .env にありません（$tf_var は Terraform が生成した値を使います）" >&2
    continue
  fi
  if [[ "$value" == "$example" ]]; then
    echo "スキップ: $env_key が .env.example のサンプル値のままです（$tf_var は Terraform が生成した値を使います）" >&2
    continue
  fi

  lines+=("$tf_var = \"$(escape_hcl "$value")\"")
  echo "書き出し: $env_key -> $tf_var"
done

umask 077
{
  echo "# scripts/env-to-tfvars.sh が .env から生成したファイル。コミットしないこと"
  for line in "${lines[@]+"${lines[@]}"}"; do
    echo "$line"
  done
} > "$out_file"

echo "作成しました: $out_file"
