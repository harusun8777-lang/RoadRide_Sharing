# RoadRide Sharing

地域AI乗り合い交通サービスのMVPです。同じ方向へ移動する利用者をまとめ、乗合タクシーを効率的に配車するWebアプリを目指しています。

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
│   ├── domain/          # ドメインモデル（User・Rider・Driver, Reservation, RideGroup と状態遷移ルール）
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

## 設計資料

- [要件定義書](docs/requirements.md)
- [API仕様書](docs/api-specification.md)
- [データモデル](docs/data-model.md)
- [画面遷移](docs/screen-transition.md)
- [UIデザインガイドライン](docs/ui-design-guidelines.md)
- [ペルソナ](docs/personas.md)
- [1週間MVP計画](docs/one-week-mvp-plan.md)
