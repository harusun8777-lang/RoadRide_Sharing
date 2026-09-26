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
├── infra/               # Azure 本番環境の Terraform（手順は infra/README.md）
├── frontend/            # フロントエンド（静的な HTML/CSS/JS）
├── cloudflare/          # フロントエンドを配信し /api を中継する Cloudflare Worker（手順は cloudflare/README.md）
├── docker-compose.yml
└── .env.example
```

依存の向きは `handler → usecase → domain` で、`infrastructure` が `usecase` のリポジトリインターフェースを実装します。

## セットアップ

### 前提

- Docker / Docker Compose
- ローカルで直接動かす場合は .NET 10 SDK

### start-local.bat で起動する（Windows）

SQL Server・バックエンド・Swagger UI をまとめて起動し、起動したらブラウザで Swagger UI を開きます。

```bat
start-local.bat         :: 起動
start-local.bat stop    :: 停止（DB のデータは残る）
```

| URL | 内容 |
| --- | --- |
| http://localhost:8080 | API |
| http://localhost:8081 | Swagger UI（`docs/SWAGGER.yaml` を表示。「Try it out」で API を呼べる） |

- `.env` がなければ `.env.example` からコピーします。`JWT_SIGNING_KEY` は 32 文字以上の値に変更してください
- Swagger UI から API を呼べるよう、Development 環境のときだけ `http://localhost:8081` からの CORS を許可しています
- ログインで受け取った `access_token` を、Swagger UI 右上の「Authorize」に入れると認証が必要な API も試せます

### Docker Compose で起動する

```sh
cp .env.example .env        # 必要に応じて MSSQL_SA_PASSWORD を変更
docker compose up --build
```

- API: http://localhost:8080
- ヘルスチェック: http://localhost:8080/health（認証不要。DB に接続できれば 200、できなければ 503）
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

### 認証（JWT）

メールアドレスとパスワードでログインし、発行された JWT を `Authorization: Bearer <token>` ヘッダーで送ります。

1. `POST /api/users` で利用者登録（パスワードは8〜128文字）
2. `POST /api/auth/login` でログインし、レスポンスの `access_token` を受け取る
3. ほかの `/api` エンドポイントは、この JWT を付けて呼び出す（有効期限は60分）

JWT の署名鍵は設定 `Jwt:SigningKey`（32バイト以上）で指定します。未設定または短すぎる場合は起動時にエラーになります。

| 実行方法 | 署名鍵の指定方法 |
| --- | --- |
| Docker Compose | `.env` の `JWT_SIGNING_KEY` |
| `dotnet run`（Development） | `appsettings.Development.json` の開発用の値 |

本番環境では、十分に長いランダムな値を環境変数 `Jwt__SigningKey` で渡してください。

Bruno で試す場合は、Login リクエストで取得した `access_token` をコレクション変数 `accessToken` に設定してください。

### データベーススキーマ

マイグレーションは未導入です。起動時に `EnsureCreated()` でDB・テーブルを作成します（既に存在する場合は何もしません）。

そのため **エンティティを変更してもスキーマは自動更新されません**。モデルを変更した場合はDBを作り直してください。

```sh
docker compose down -v   # ボリュームごと削除（データも消えます）
docker compose up --build
```

## テスト

単体テストは `tests/backend.Tests/`（xUnit）にあります。層ごとのテスト設計書は `backend/{domain,usecase,handler,infrastructure}/TESTING.md` です。

```sh
dotnet test RoadRideSharing.slnx                                   # すべて（Docker が必要）
dotnet test RoadRideSharing.slnx --filter "Category!=Database"     # Docker なしで動くテストだけ
```

- infrastructure 層の DB テスト（`Category=Database`）は Testcontainers で SQL Server 2022 のコンテナを起動します
- PR と main への push で GitHub Actions（`.github/workflows/test-backend.yml`）がすべてのテストを実行します

## 設計資料

- [要件定義書](docs/REQUIREMENTS.md)
- [API エンドポイント一覧](docs/ENDPOINT.md) / [OpenAPI 定義（Swagger）](docs/SWAGGER.yaml)
- [データモデル](docs/DATA_MODEL.md)
- [画面遷移](docs/SCREEN_TRANSITION.md)
- [UIデザインガイドライン](docs/UI_DESIGN_GUIDELINES.md)
- [ペルソナ](docs/PERSONAS.md)
- [1週間MVP計画](docs/ONE_WEEK_MVP_PLAN.md)
