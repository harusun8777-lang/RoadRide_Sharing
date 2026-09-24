# RoadRide Sharing

地域AI乗り合い交通サービスのMVPです。同じ方向へ移動する利用者をまとめ、乗合タクシーを効率的に配車するWebアプリを目指しています。

> 現在はバックエンドの **利用者API** と **予約API** のみ実装済みです。乗合候補作成・配車確定・通知などは未実装です（[実装状況](#実装状況)を参照）。

## 技術スタック

| 区分 | 内容 |
| --- | --- |
| バックエンド | ASP.NET Core 10（Minimal API） |
| ORM | Entity Framework Core 10（SQL Server プロバイダ） |
| データベース | SQL Server 2022（Docker） |
| APIテスト | [Bruno](https://www.usebruno.com/)（`backend/bruno`） |
| 実行環境 | Docker Compose |

## ディレクトリ構成

```
.
├── backend/
│   ├── domain/          # ドメインモデル（User, Reservation と状態遷移ルール）
│   ├── usecase/         # ユースケースとリポジトリのインターフェース
│   ├── infrastructure/  # EF Core による永続化（AppDbContext, Ef*Repository）
│   ├── handler/         # HTTPエンドポイント（リクエスト/レスポンスの変換）
│   ├── bruno/           # Bruno の APIリクエスト集
│   ├── Program.cs       # DI登録・起動処理
│   └── Dockerfile
├── docs/                # 要件定義・API仕様・データモデル等の設計資料
├── docker-compose.yml
└── .env.example
```

依存の向きは `handler → usecase → domain` で、`infrastructure` が `usecase` のリポジトリインターフェースを実装します。

## セットアップ

### 前提

- Docker / Docker Compose
- ローカルで直接動かす場合は .NET 10 SDK

### Docker Compose で起動する（推奨）

```sh
cp .env.example .env        # 必要に応じて MSSQL_SA_PASSWORD を変更
docker compose up --build
```

- API: http://localhost:5087
- SQL Server: `localhost:1433`（ユーザー `sa` / パスワードは `.env` の `MSSQL_SA_PASSWORD`）

SQL Server のヘルスチェックが通ってからバックエンドが起動します。コードを変更した場合は `--build` を付けて再ビルドしてください。

### ローカルで dotnet run する

DBだけコンテナで起動し、バックエンドはホストで動かします。

```sh
docker compose up -d sqlserver
cd backend
dotnet run
```

接続文字列は `backend/appsettings.json` の `ConnectionStrings:DefaultConnection` を使います。`.env` のパスワードを変更した場合は、環境変数で上書きしてください。

```sh
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=RoadRideSharing;User Id=sa;Password=<パスワード>;TrustServerCertificate=True"
```

### データベーススキーマ

マイグレーションは未導入です。起動時に `EnsureCreated()` でDB・テーブルを作成します（既に存在する場合は何もしません）。

そのため **エンティティを変更してもスキーマは自動更新されません**。モデルを変更した場合はDBを作り直してください。

```sh
docker compose down -v   # ボリュームごと削除（データも消えます）
docker compose up --build
```

## API

- ベースパス: `/api`
- 形式: JSON（UTF-8）、プロパティ名は snake_case
- 日時: ISO 8601。DBにはUTCで保存します
- 認証: MVPでは未実装
- 開発環境では OpenAPI 定義を `/openapi/v1.json` で取得できます

### 実装済みエンドポイント

| メソッド | パス | 用途 |
| --- | --- | --- |
| `POST` | `/api/users` | 利用者登録 |
| `GET` | `/api/users/{user_id}` | 利用者取得 |
| `POST` | `/api/reservations` | 予約登録 |
| `GET` | `/api/reservations` | 予約一覧取得 |
| `GET` | `/api/reservations/{reservation_id}` | 予約詳細取得 |
| `POST` | `/api/reservations/{reservation_id}/cancel` | 予約キャンセル |

### 利用例

```sh
# 利用者登録（role は rider / dispatcher）
curl -X POST http://localhost:5087/api/users \
  -H 'Content-Type: application/json' \
  -d '{"name": "山田太郎", "role": "rider"}'

# 予約登録
curl -X POST http://localhost:5087/api/reservations \
  -H 'Content-Type: application/json' \
  -d '{
    "user_id": "<利用者ID>",
    "pickup_location": "東京駅",
    "destination": "羽田空港",
    "requested_pickup_at": "2026-10-01T09:00:00+09:00",
    "passenger_count": 2,
    "consideration_notes": "車椅子を利用します"
  }'

# 予約一覧（絞り込み）
curl "http://localhost:5087/api/reservations?user_id=<利用者ID>&status=matching&page=1&limit=50"

# 予約キャンセル
curl -X POST http://localhost:5087/api/reservations/<予約ID>/cancel \
  -H 'Content-Type: application/json' \
  -d '{"reason": "予定変更のため"}'
```

### 予約一覧のクエリパラメータ

| パラメータ | 説明 |
| --- | --- |
| `user_id` | 利用者IDで絞り込み |
| `date` | 乗車日（`YYYY-MM-DD`）。**日本時間の暦日**として判定します |
| `status` | `matching` / `confirmed` / `in_progress` / `completed` / `cancelled` |
| `from`, `to` | 希望乗車日時の範囲 |
| `page`, `limit` | ページング（既定値: `page=1`, `limit=50`） |

結果は希望乗車日時の昇順で返り、`meta` に `page` / `limit` / `total` が含まれます。

### 予約の状態

```
matching（配車調整中） → confirmed（配車確定） → in_progress（乗車中） → completed（完了）
matching / confirmed → cancelled（キャンセル）
```

キャンセルできるのは `matching` と `confirmed` の予約のみです。

### エラーレスポンス

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "入力内容を確認してください",
    "details": [{ "field": "user_id", "message": "指定された利用者が見つかりません" }]
  }
}
```

| ステータス | code | 主なケース |
| --- | --- | --- |
| 422 | `VALIDATION_ERROR` | 入力値不正、存在しない利用者IDでの予約 |
| 404 | `NOT_FOUND` | 利用者・予約が存在しない |
| 409 | `CONFLICT` | キャンセル済みなど、状態遷移できない予約の操作 |

詳細な仕様は [docs/api-specification.md](docs/api-specification.md) を参照してください（未実装のエンドポイントも含みます）。

### Bruno で試す

Bruno で `backend/bruno` フォルダを開き、環境 `Local`（`baseUrl = http://localhost:5087`）を選択してリクエストを実行します。

## 実装状況

| 機能 | 状況 |
| --- | --- |
| 利用者の登録・取得 | ✅ 実装済み |
| 予約の登録・一覧・詳細・キャンセル | ✅ 実装済み |
| SQL Server への永続化 | ✅ 実装済み |
| 予約状態変更（`PATCH /status`） | 未実装 |
| 乗合候補作成・確定前チェック | 未実装 |
| 乗合グループの編集・配車確定 | 未実装 |
| 通知 | 未実装 |
| フロントエンド | 未実装（`ui-preview.html` はUIの試作） |
| 認証・権限 | MVP対象外 |
| DBマイグレーション | 未導入（`EnsureCreated` で代替） |

## 設計資料

- [要件定義書](docs/requirements.md)
- [API仕様書](docs/api-specification.md)
- [データモデル](docs/data-model.md)
- [画面遷移](docs/screen-transition.md)
- [UIデザインガイドライン](docs/ui-design-guidelines.md)
- [ペルソナ](docs/personas.md)
- [1週間MVP計画](docs/one-week-mvp-plan.md)
