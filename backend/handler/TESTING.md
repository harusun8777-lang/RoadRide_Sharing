# handler 層 単体テスト設計書

`backend/handler/` の HTTP エンドポイントに対する単体テストの設計です。
API の仕様は [docs/ENDPOINT.md](../../docs/ENDPOINT.md) と [docs/SWAGGER.yaml](../../docs/SWAGGER.yaml) を正とし、食い違いは「[5. 既知の問題・仕様の曖昧な点](#5-既知の問題仕様の曖昧な点)」にまとめます。

## 目次

- [1. 目的と範囲](#1-目的と範囲)
- [2. テスト方針](#2-テスト方針)
- [3. テスト対象一覧](#3-テスト対象一覧)
- [4. テストケース](#4-テストケース)
- [5. 既知の問題・仕様の曖昧な点](#5-既知の問題仕様の曖昧な点)
- [6. 対象外](#6-対象外)

## 1. 目的と範囲

### 目的

handler 層が、HTTP リクエストを usecase の呼び出しに正しく変換し、その結果（戻り値・例外）を API 仕様どおりの HTTP レスポンスに変換していることを確認します。

確認する観点は次のとおりです。

- ステータスコード（200 / 201 / 400 / 401 / 404 / 409 / 422 / 503）
- レスポンスの形（`data` / `meta` / `error`）と JSON のキー名（スネークケース）
- エラーコード（`VALIDATION_ERROR` / `INVALID_CREDENTIALS` / `NOT_FOUND` / `CONFLICT`）と `details[].field`
- 列挙値の変換（`rider` / `driver`、`matching` 〜 `cancelled`）
- 認証（JWT）と、本人（`sub`）のデータだけを扱うこと
- `Location` ヘッダー、`/health` の `Cache-Control`

### 範囲

| 対象 | ファイル |
| --- | --- |
| エンドポイント | `AuthHandler.cs`、`UserHandler.cs`、`ReservationHandler.cs`、`HealthHandler.cs` |
| 補助 | `CurrentUser.cs`、`dto/*.cs` |
| パイプライン設定 | `backend/Program.cs`（JWT 認証、`/api` グループの `RequireAuthorization()`、DI 登録） |

## 2. テスト方針

### ツール

| 用途 | 使うもの |
| --- | --- |
| テストフレームワーク | xUnit |
| アサーション | xUnit 標準の `Assert`（FluentAssertions は v8 から商用ライセンスのため使わない） |
| HTTP 経由の呼び出し | `Microsoft.AspNetCore.Mvc.Testing` の `WebApplicationFactory<Program>` |
| JSON の検証 | `System.Text.Json.JsonDocument`（キー名をそのまま確認するため、DTO へ逆シリアル化しない） |
| JWT の発行 | `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler`（本体が参照済み） |

### テストプロジェクトの構成

`backend/` の中に置くと `backend.csproj` が `**/*.cs` を取り込むため、リポジトリ直下の `tests/` に置きます。

```
tests/backend.Tests/
├── backend.Tests.csproj      # ProjectReference: ../../backend/backend.csproj
├── Domain/                   # 別担当
├── Usecase/                  # 別担当
├── Infrastructure/           # 別担当
├── Handler/
│   ├── ApiFactory.cs         # WebApplicationFactory<Program> の派生
│   ├── TestJwt.cs            # テスト用 JWT の発行
│   ├── ApiAssert.cs          # JSON の読み取り・キーの集合・エラー形式の検証などの補助
│   ├── ProgramTests.cs       # 認証・認可（Program.cs の設定）
│   ├── HealthHandlerTests.cs
│   ├── AuthHandlerTests.cs
│   ├── UserHandlerTests.cs
│   ├── ReservationHandlerTests.cs
│   └── CurrentUserTests.cs
└── TestDoubles/              # usecase 層と共用の手書き Fake
    ├── FakeUserRepository.cs
    ├── FakeReservationRepository.cs
    ├── FakeRideGroupRepository.cs
    └── FakeHealthCheck.cs
```

- テストクラス名は `{対象クラス}Tests`、テストメソッド名は `{メソッド}_{条件}_{期待結果}`（例：`RegisterAsync_ロールが不正_422を返す`）
- 認証まわりは `Program.cs` の設定が対象なので `ProgramTests` に置く（メソッド名は `Authentication_トークンなし_401を返す` のように書く）

### テストダブルの方針

handler は具象の UseCase クラス（`LoginUseCase` など）に直接依存しているため、UseCase は差し替えず**本物を DI から使います**。
差し替えるのは UseCase が依存するリポジトリと、DB に触れる部品だけです。

| DI の登録 | テストでの扱い |
| --- | --- |
| `IUserRepository` | `FakeUserRepository`（シングルトンで登録） |
| `IReservationRepository` | `FakeReservationRepository`（同上） |
| `IRideGroupRepository` | `FakeRideGroupRepository`（同上） |
| ヘルスチェック `database`（`DatabaseHealthCheck`） | `FakeHealthCheck` に差し替える（下記） |
| `AppDbContext` | 使わない。登録ごと削除し、誤って使えば例外になるようにする |
| `IPasswordHasher`（`AspNetPasswordHasher`） | 本物を使う（外部依存がないため） |
| `IAccessTokenIssuer`（`JwtAccessTokenIssuer`） | 本物を使う（ログインで発行したトークンで保護 API を呼べることを確認するため） |
| UseCase 各クラス | 本物を使う |

`AppDbContext` は具象の EF Core クラスで、使っているのは EF のリポジトリ・`DatabaseHealthCheck`・起動時の `EnsureCreated()` だけです。前の 2 つを差し替え、`EnsureCreated()` を止めれば（[前提となる変更](#前提となる変更)）不要になるため、Fake は作りません。

#### handler 層が Fake に求める機能

usecase 層の担当と同じ Fake を使うので、次の機能があればよい（名前は実装時に揃える）。

| Fake | 必要な機能 |
| --- | --- |
| `FakeUserRepository` | 事前にユーザーを入れる（`Seed`）。`FindByIdAsync` / `FindByEmailAsync` / `AddAsync` / `UpdateAsync` を辞書で実装。保存後の状態を取り出せる |
| `FakeReservationRepository` | 事前に予約を入れる。`FindByIdAsync` / `AddAsync` / `UpdateAsync`。`HasUnfinishedAsync` の結果を指定できる。`ListAsync` は**受け取ったフィルターを記録**し（`LastListFilter`）、返す一覧と件数を指定できる |
| `FakeRideGroupRepository` | `HasUnfinishedByDriverAsync` の結果を指定できる |
| `FakeHealthCheck` | 返す `HealthCheckResult` を指定できる（既定は Healthy） |

テストデータは domain の公開メソッドで作ります（`User.Create` → `AddRole`、`Reservation.Create` → `Confirm` / `StartInProgress` / `Complete` / `Cancel`）。パスワードは `AspNetPasswordHasher.Hash` でハッシュ化して入れます。

### WebApplicationFactory の設定（`ApiFactory`）

`ConfigureWebHost` で次を行います。

| 設定 | 内容 | 理由 |
| --- | --- | --- |
| 環境名 | `UseEnvironment("Testing")` | Development 専用の CORS・OpenAPI・開発者例外ページを外し、400 の応答を安定させる |
| JWT | `UseSetting("Jwt:Issuer", …)`、`Jwt:Audience`、`Jwt:SigningKey`（64 バイト以上のテスト用の鍵） | `Program.cs` は `Build()` 前に `Jwt` セクションを読んで `Validate()` するため。`ConfigureAppConfiguration` で足した値はこの読み取りに間に合わない場合があるので `UseSetting` を使う。鍵を 64 バイト以上にするのは HS512 のトークン（H-007）を作れるようにするため |
| 起動時のスキーマ作成 | `UseSetting("Database:EnsureCreatedOnStartup", "false")` | [前提となる変更](#前提となる変更) を参照 |
| リポジトリ | `ConfigureTestServices` で `RemoveAll<IUserRepository>()` などを行い、Fake のインスタンスを `AddSingleton` | DB を使わない |
| ヘルスチェック | `services.Configure<HealthCheckServiceOptions>(o => …)` で名前が `database` の登録の `Factory` を `_ => FakeHealthCheck` に置き換える | `AddCheck<DatabaseHealthCheck>` は型で登録されているため、DI の差し替えでは置き換わらない。名前 `database` はレスポンスに出るので残す |
| `AppDbContext` | `AppDbContext` と `DbContextOptions<AppDbContext>` の登録を削除 | DB に触れていないことを保証する |

- xUnit は同じクラスのテストを順に、別クラスは並列に実行するため、`ApiFactory` は `IClassFixture<ApiFactory>` でクラスごとに持つ。Fake の状態はテストクラスのコンストラクターでリセットする（`ApiFactory.ResetFakes()`）
- `app.UseHttpsRedirection()` は HTTPS ポートが決まらない限りリダイレクトしない。テストで `https_port` などを設定しないこと

### JWT の発行（`TestJwt`）

- 正常なトークンは本体の `JwtAccessTokenIssuer` に `Options.Create(new JwtOptions { … })`（`ApiFactory` と同じ値）を渡して発行する。`sub` はユーザーID
- 異常系のトークンは `JsonWebTokenHandler.CreateToken(SecurityTokenDescriptor)` で直接作り、次を引数で変えられるようにする
  - 署名鍵（別の鍵）、署名アルゴリズム（`HmacSha512`）
  - `iss` / `aud`、`exp`（過去の時刻。`NotBefore` / `IssuedAt` も合わせて過去にする）
  - `sub`（なし、GUID でない文字列、存在しないユーザーの GUID）
- ヘッダーは `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token)`

### 検証の書き方

- ステータスは `Assert.Equal(HttpStatusCode.X, response.StatusCode)`
- ボディは `JsonDocument` で読み、キー名は文字列のまま確認する。レスポンスの形を確認するケースでは、キーの集合が過不足なく一致することを確認する（`EnumerateObject().Select(p => p.Name)` を期待値の集合と比べる）
- `error.details` は `VALIDATION_ERROR` 以外では**キー自体がない**ことを確認する（`TryGetProperty` が `false`）
- 日時は文字列の末尾が `Z` であることと、値（`DateTimeOffset` で比較）を確認する
- 「本文なし」は `Content.ReadAsStringAsync()` が空文字であることで確認する

### テストの書き方の例

```csharp
public class ReservationHandlerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReservationHandlerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetFakes();
    }

    [Fact]
    public async Task GetAsync_他人の予約_404とNOT_FOUNDを返す()
    {
        var me = TestUsers.Rider();
        var other = TestUsers.Rider();
        _factory.Users.Seed(me, other);
        var reservation = TestReservations.Matching(other.Id);
        _factory.Reservations.Seed(reservation);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken(me));

        var response = await client.GetAsync($"/api/reservations/{reservation.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = json.RootElement.GetProperty("error");
        Assert.Equal("NOT_FOUND", error.GetProperty("code").GetString());
        Assert.Equal("指定された予約が見つかりません", error.GetProperty("message").GetString());
        Assert.False(error.TryGetProperty("details", out _));
    }
}
```

### この層で書かないこと

- domain の検証ルールの網羅（カタカナの判定、パスワードの文字数の境界など）。handler 層では、代表的な値で `422` と `field` への変換だけを確認する
- usecase の分岐の網羅（未完了の判定条件など）。handler 層では、例外が正しいステータスとメッセージになることだけを確認する
- リポジトリの検索条件（日本時間での日付の絞り込み、ページングの補正、並び順）。handler 層では、クエリがフィルターに正しく渡ることだけを確認する
- `JwtAccessTokenIssuer` / `JwtOptions` / `AspNetPasswordHasher` / `DatabaseHealthCheck` 自体の動作（infrastructure 層で確認する）

### 前提となる変更

テストを書く前に、本体に次の変更が必要です（どちらも対応済み）。

| # | 変更 | 理由 |
| --- | --- | --- |
| 1 | `Program.cs` の末尾に `public partial class Program { }` を追加する | トップレベルステートメントの `Program` クラスは既定で `internal` のため、別アセンブリのテストから `WebApplicationFactory<Program>` と書けない。ASP.NET Core 10 ではソースジェネレーターで自動的に公開されるとされているが、ビルドで確認できるまでは明示しておく（不要ならアナライザーが警告する） |
| 2 | 起動時の `db.Database.EnsureCreated()` を、設定値（例：`Database:EnsureCreatedOnStartup`、既定は `true`）が `false` のときに呼ばないようにする | テストでは SQL Server がないため、そのままでは起動時に接続エラーで止まる。本番・ローカルの動作は変わらない |

2 を入れない場合の代替として、テスト側で `DbContextOptions<AppDbContext>` を EF Core InMemory プロバイダー（`Microsoft.EntityFrameworkCore.InMemory` をテストプロジェクトに追加）に差し替え、`EnsureCreated()` を空振りさせる方法もあります。ただし「DB を使わない」方針から外れるため、1・2 の対応を推奨します。

## 3. テスト対象一覧

| メソッド | パス | 実装 | 認証 | 成功時 | テストクラス |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/health` | `HealthHandler.MapHealthEndpoints` / `WriteResponseAsync` | 不要 | 200（異常時 503） | `HealthHandlerTests` |
| `POST` | `/api/auth/login` | `AuthHandler.LoginAsync` | 不要 | 200 | `AuthHandlerTests` |
| `POST` | `/api/users` | `UserHandler.RegisterAsync` | 不要 | 201 | `UserHandlerTests` |
| `GET` | `/api/users/me` | `UserHandler.GetMeAsync` | 必要 | 200 | `UserHandlerTests` |
| `POST` | `/api/users/me/roles` | `UserHandler.AddRoleAsync` | 必要 | 200 | `UserHandlerTests` |
| `PUT` | `/api/users/me/active-role` | `UserHandler.SwitchRoleAsync` | 必要 | 200 | `UserHandlerTests` |
| `POST` | `/api/reservations` | `ReservationHandler.RegisterAsync` | 必要 | 201 | `ReservationHandlerTests` |
| `GET` | `/api/reservations` | `ReservationHandler.ListAsync` | 必要 | 200 | `ReservationHandlerTests` |
| `GET` | `/api/reservations/{reservationId:guid}` | `ReservationHandler.GetAsync` | 必要 | 200 | `ReservationHandlerTests` |
| `POST` | `/api/reservations/{reservationId:guid}/cancel` | `ReservationHandler.CancelAsync` | 必要 | 200 | `ReservationHandlerTests` |

| クラス | 内容 | テストクラス |
| --- | --- | --- |
| `Program`（認証の設定） | JWT の検証パラメーター、`/api` グループの `RequireAuthorization()`、`AllowAnonymous()` | `ProgramTests` |
| `CurrentUser` | `ClaimsPrincipal` から `sub` を取り出す | `CurrentUserTests`（HTTP を使わない純粋な単体テスト） |
| `dto/*.cs` | JSON のキー名、`details` の省略 | 各エンドポイントのテストで確認（DTO 単体のテストは書かない） |

### 列挙値の変換

| 区分 | domain | JSON |
| --- | --- | --- |
| 利用者区分 | `UserRole.Rider` / `Driver` | `rider` / `driver` |
| 予約状態 | `ReservationStatus.Matching` / `Confirmed` / `InProgress` / `Completed` / `Cancelled` | `matching` / `confirmed` / `in_progress` / `completed` / `cancelled` |

リクエスト側の変換（`TryParseRole` / `TryParseStatus`）は大文字小文字を区別し、上記以外は `422` です。

## 4. テストケース

「保護 API 7 本」は `GET /api/users/me`、`POST /api/users/me/roles`、`PUT /api/users/me/active-role`、`POST /api/reservations`、`GET /api/reservations`、`GET /api/reservations/{id}`、`POST /api/reservations/{id}/cancel` を指します。
401 のテストでは、ボディを取るエンドポイントに**正しいボディ**を付けます（ボディの読み取りはハンドラーの本人確認より先に行われるため。[5. 既知の問題](#5-既知の問題仕様の曖昧な点) の 3）。
「ボディなし」は、Content 自体を付けない場合と、`Content-Type: application/json` の空ボディの場合の両方を確認します（どちらも 400。`Content-Type` が JSON でないボディは 415 になるが、ケースにしない）。

### 4.1 認証・認可（`ProgramTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-001 | 保護 API 7 本 | 認証なし | `Authorization` ヘッダーなし（Theory で 7 本） | 401。本文なし。`WWW-Authenticate: Bearer` | 高 |
| H-002 | `GET /api/users/me` | 不正トークン：署名 | 別の鍵で署名したトークン | 401。本文なし | 高 |
| H-003 | `GET /api/users/me` | 不正トークン：期限切れ | `exp` が 5 分前 | 401。本文なし | 高 |
| H-004 | `GET /api/users/me` | 期限の許容誤差 | `exp` が 10 秒前（`ClockSkew` 30 秒の内側） | 200 | 低 |
| H-005 | `GET /api/users/me` | 不正トークン：発行者 | `iss` が設定と違う | 401 | 中 |
| H-006 | `GET /api/users/me` | 不正トークン：対象者 | `aud` が設定と違う | 401 | 中 |
| H-007 | `GET /api/users/me` | 不正トークン：アルゴリズム | 同じ鍵で HS512 署名（`ValidAlgorithms` は HS256 のみ） | 401 | 中 |
| H-008 | `GET /api/users/me` | 不正トークン：形式 | `Bearer abc`、`Basic xxx` | 401 | 低 |
| H-009 | `GET /api/users/me` | `sub` なし | 署名は正しく `sub` がない | 401。本文なし（ハンドラーの `Results.Unauthorized()` のため `WWW-Authenticate` は確認しない） | 中 |
| H-010 | `GET /api/users/me` | `sub` が GUID でない | `sub = "abc"` | 401。本文なし | 中 |
| H-011 | 保護 API 7 本 | ユーザー不在 | 正しいトークン。`sub` の GUID が Fake にない（Theory で 7 本） | 401。本文なし | 高 |
| H-012 | `POST /api/auth/login`、`POST /api/users`、`GET /health` | 匿名許可 | トークンなし、正しい入力 | それぞれ 200 / 201 / 200（401 にならない） | 高 |
| H-013 | 同上 | 匿名許可に不正トークン | 別の鍵で署名したトークンを付ける | 401 にならない（200 / 201 / 200） | 低 |

### 4.2 GET /health（`HealthHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-014 | `WriteResponseAsync` | 正常 | `FakeHealthCheck` が Healthy | 200。ボディのキーは `status` / `checks` のみ。`{"status":"Healthy","checks":{"database":{"status":"Healthy","description":null}}}`（`description` は `null` でキーあり） | 高 |
| H-015 | `WriteResponseAsync` | 異常 | Unhealthy（説明 `データベースに接続できません`） | 503。`status` = `Unhealthy`、`checks.database.status` = `Unhealthy`、`checks.database.description` = `データベースに接続できません` | 高 |
| H-016 | `WriteResponseAsync` | キャッシュ禁止 | Healthy / Unhealthy（Theory） | `Cache-Control` の `NoStore` が `true`（200・503 とも） | 高 |
| H-017 | `WriteResponseAsync` | Content-Type | Healthy | `Content-Type` が `application/json` | 低 |
| H-018 | `MapHealthEndpoints` | Degraded の扱い | Degraded | 200。`status` = `Degraded`（ASP.NET Core の既定の対応付け） | 低 |

### 4.3 POST /api/auth/login（`AuthHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-019 | `LoginAsync` | 正常 | 登録済みユーザーの `email` / `password` | 200。`data` のキーは `access_token` / `token_type` / `expires_at` のみ。`token_type` = `Bearer`、`access_token` は空でない、`expires_at` は末尾 `Z` で現在 + 60 分前後 | 高 |
| H-020 | `LoginAsync` | 発行トークンの中身 | H-019 のトークンを `JsonWebTokenHandler.ReadJsonWebToken` で読む | `sub` = ユーザーID、`iss` / `aud` = 設定値、`alg` = `HS256` | 中 |
| H-021 | `LoginAsync` | 発行トークンで保護 API を呼べる | H-019 のトークンで `GET /api/users/me` | 200。`data.id` = ログインしたユーザー | 高 |
| H-022 | `LoginAsync` | メールアドレスの前後の空白 | `email` = `" taro@example.com "` | 200 | 中 |
| H-023 | `LoginAsync` | パスワード違い | 登録済みの `email`、違う `password` | 401。`error.code` = `INVALID_CREDENTIALS`、`error.message` = `メールアドレスまたはパスワードが正しくありません`、`details` キーなし | 高 |
| H-024 | `LoginAsync` | 未登録 | 未登録の `email` | 401。H-023 と同じボディ（原因を区別しない） | 高 |
| H-025 | `LoginAsync` | 空の入力 | `email` / `password` が `""`・空白のみ・`null`・キーなし（Theory） | 401 `INVALID_CREDENTIALS`（422 ではない） | 中 |
| H-026 | `LoginAsync` | ボディ不正 | ボディなし／JSON として不正 | 400 | 中 |

### 4.4 POST /api/users（`UserHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-027 | `RegisterAsync` | 正常（rider） | 正しい入力、`role` = `rider` | 201。`Location: /api/users/me`。`data.active_role` = `rider`、`data.roles` = `["rider"]`、`data.id` は GUID、`created_at` は末尾 `Z`。Fake に保存されている | 高 |
| H-028 | `RegisterAsync` | 正常（driver） | `role` = `driver` | 201。`active_role` = `driver`、`roles` = `["driver"]` | 高 |
| H-029 | `RegisterAsync` | JSON のキー名 | H-027 のレスポンス | `data` のキーが `id` / `email` / `first_name` / `last_name` / `kana_first_name` / `kana_last_name` / `active_role` / `roles` / `created_at` と過不足なく一致（`password`・`password_hash` がない） | 高 |
| H-030 | `RegisterAsync` | 区分が不正 | `role` = `admin`・`Rider`・`""`・キーなし（Theory） | 422。`error.code` = `VALIDATION_ERROR`、`error.message` = `入力内容を確認してください`、`details` = `[{"field":"role","message":"利用者区分の値が不正です"}]` | 高 |
| H-031 | `RegisterAsync` | パスワードの長さ | `password` が 7 文字 | 422。`details[0].field` = `password` | 高 |
| H-032 | `RegisterAsync` | メールアドレス重複 | Fake に同じ `email` のユーザーがいる | 409。`error.code` = `CONFLICT`、`error.message` = `このメールアドレスはすでに登録されています`、`details` キーなし | 高 |
| H-033 | `RegisterAsync` | 必須項目が空 | `email` / `last_name` / `first_name` が空白のみ（Theory） | 422。`details[0].field` = `email` / `lastName` / `firstName`（キャメルケース） | 中 |
| H-034 | `RegisterAsync` | 読み仮名が不正 | `kana_last_name` / `kana_first_name` がひらがな（Theory） | 422。`details[0].field` = `kanaLastName` / `kanaFirstName`、`details[0].message` は英語（`… must be full-width katakana. (Parameter '…')`） | 中 |
| H-035 | `RegisterAsync` | チェックの順序 | `role` 不正かつ `password` 7 文字 | 422。`details` は `role` の 1 件だけ | 中 |
| H-036 | `RegisterAsync` | チェックの順序 | `password` 7 文字かつ `email` 重複 | 422（`password`）。409 ではない | 低 |
| H-037 | `RegisterAsync` | 前後の空白の除去 | `last_name` = `" 山田 "` | 201。`data.last_name` = `山田` | 低 |
| H-038 | `RegisterAsync` | ボディ不正 | ボディなし／JSON として不正 | 400 | 低 |

### 4.5 GET /api/users/me（`UserHandlerTests`）

認証なし・不正トークン・ユーザー不在は H-001〜H-011 で確認します。

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-039 | `GetMeAsync` | 正常 | rider のユーザーのトークン | 200。`data` はユーザー情報（キーは H-029 と同じ）。値が Fake のユーザーと一致 | 高 |
| H-040 | `GetMeAsync` | 区分を両方持つ | rider + driver、稼働は driver | 200。`roles` = `["rider","driver"]`、`active_role` = `driver` | 中 |
| H-041 | `GetMeAsync` | 本人のデータだけ | Fake にユーザー A・B。A のトークン | `data.id` = A | 中 |

### 4.6 POST /api/users/me/roles（`UserHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-042 | `AddRoleAsync` | 正常 | rider のユーザー、`{"role":"driver"}` | 200。`roles` = `["rider","driver"]`、`active_role` = `rider` のまま。Fake が更新されている | 高 |
| H-043 | `AddRoleAsync` | 登録済みの区分 | rider のユーザー、`{"role":"rider"}` | 409。`CONFLICT`、`この利用者区分は登録済みです` | 高 |
| H-044 | `AddRoleAsync` | 区分が不正 | `{"role":"admin"}` | 422。`details` = `[{"field":"role","message":"利用者区分の値が不正です"}]` | 高 |
| H-045 | `AddRoleAsync` | 本人確認が先 | ユーザー不在のトークン、`{"role":"admin"}` | 401（422 ではない） | 中 |
| H-046 | `AddRoleAsync` | ボディ不正 | ボディなし | 400 | 低 |

### 4.7 PUT /api/users/me/active-role（`UserHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-047 | `SwitchRoleAsync` | 正常 | rider + driver、稼働 rider、未完了の予約なし。`{"role":"driver"}` | 200。`active_role` = `driver`。Fake が更新されている | 高 |
| H-048 | `SwitchRoleAsync` | 同じ区分 | 稼働 rider、未完了の予約あり。`{"role":"rider"}` | 200。`active_role` = `rider`（未完了があっても成功） | 中 |
| H-049 | `SwitchRoleAsync` | 未完了の予約 | 稼働 rider、`FakeReservationRepository` の未完了 = あり。`{"role":"driver"}` | 409。`CONFLICT`、`完了またはキャンセルされていない予約があるため切り替えできません` | 高 |
| H-050 | `SwitchRoleAsync` | 未完了の運行 | 稼働 driver、`FakeRideGroupRepository` の未完了 = あり。`{"role":"rider"}` | 409。`CONFLICT`、`完了またはキャンセルされていない運行があるため切り替えできません` | 高 |
| H-051 | `SwitchRoleAsync` | 未登録の区分 | rider のみのユーザー。`{"role":"driver"}` | 409。`CONFLICT`、`この利用者区分は登録されていません` | 高 |
| H-052 | `SwitchRoleAsync` | 区分が不正 | `{"role":"Driver"}` | 422。`details[0].field` = `role` | 高 |
| H-053 | `SwitchRoleAsync` | ボディ不正 | ボディなし | 400 | 低 |

### 4.8 POST /api/reservations（`ReservationHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-054 | `RegisterAsync` | 正常 | 稼働 rider。全項目あり、`requested_pickup_at` は `Z` 付き | 201。`Location` = `/api/reservations/{data.id}`。`status` = `matching`、`user_id` = 本人、`reservation_number` が `^RR-\d{8}-[0-9A-F]{4}$`、`cancellation_reason` / `cancelled_at` は `null`。Fake に保存されている | 高 |
| H-055 | `RegisterAsync` | JSON のキー名 | H-054 のレスポンス | `data` のキーが `id` / `reservation_number` / `user_id` / `pickup_location` / `destination` / `requested_pickup_at` / `passenger_count` / `consideration_notes` / `status` / `cancellation_reason` / `cancelled_at` / `created_at` と過不足なく一致（`null` の項目もキーあり） | 高 |
| H-056 | `RegisterAsync` | 日時 | `requested_pickup_at` = `2026-10-01T00:00:00Z` | `data.requested_pickup_at` = `2026-10-01T00:00:00Z`、`created_at` は末尾 `Z` | 中 |
| H-057 | `RegisterAsync` | 任意項目の省略 | `consideration_notes` なし | 201。`consideration_notes` は `null`（キーあり） | 中 |
| H-058 | `RegisterAsync` | 稼働区分が rider でない | 稼働 driver（rider も登録済みの場合を含む。Theory） | 409。`CONFLICT`、`利用者として稼働していないため予約できません`。Fake に保存されていない | 高 |
| H-059 | `RegisterAsync` | 乗車地が空 | `pickup_location` = `""`・空白のみ | 422。`details[0].field` = `pickupLocation` | 高 |
| H-060 | `RegisterAsync` | 目的地が空 | `destination` = `""` | 422。`details[0].field` = `destination` | 高 |
| H-061 | `RegisterAsync` | 乗車人数 | `passenger_count` = `0`・`-1`・キーなし（Theory） | 422。`details[0].field` = `passengerCount` | 高 |
| H-062 | `RegisterAsync` | キー名の違い | `pickupLocation`（キャメルケース）で送る | 422。`details[0].field` = `pickupLocation`（値として受け取られない） | 低 |
| H-063 | `RegisterAsync` | ボディ不正 | ボディなし／JSON として不正／`passenger_count` = `"abc"` | 400 | 中 |

### 4.9 GET /api/reservations（`ReservationHandlerTests`）

`FakeReservationRepository` に返す一覧と件数を指定し、受け取ったフィルター（`LastListFilter`）を確認します。

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-064 | `ListAsync` | 正常 | Fake が 2 件・`total` = 5 を返す。クエリなし | 200。`data` は 2 件、`meta` = `{"page":1,"limit":50,"total":5}` | 高 |
| H-065 | `ListAsync` | JSON のキー名 | H-064 のレスポンス | トップのキーは `data` / `meta`。`data[]` のキーが `id` / `reservation_number` / `pickup_location` / `destination` / `requested_pickup_at` / `passenger_count` / `status` と過不足なく一致（`user_id` などがない）。`meta` のキーは `page` / `limit` / `total` | 高 |
| H-066 | `ListAsync` | 0 件 | Fake が 0 件を返す | 200。`data` = `[]`、`meta.total` = 0 | 中 |
| H-067 | `ListAsync` | 本人に限定 | クエリに `user_id=<他人の ID>` を付ける | `LastListFilter.UserId` = 本人の ID | 高 |
| H-068 | `ListAsync` | クエリの受け渡し | `date=2026-10-01&status=confirmed&from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z&page=2&limit=20` | 200。`LastListFilter` の各値がクエリと一致。`meta.page` = 2、`meta.limit` = 20 | 高 |
| H-069 | `ListAsync` | 状態の変換（クエリ） | `status` = `matching` / `confirmed` / `in_progress` / `completed` / `cancelled`（Theory） | `LastListFilter.Status` が対応する列挙値 | 高 |
| H-070 | `ListAsync` | 状態の変換（レスポンス） | Fake が各状態の予約を 1 件ずつ返す（Theory） | `data[0].status` が `matching` / `confirmed` / `in_progress` / `completed` / `cancelled` | 高 |
| H-071 | `ListAsync` | 状態が不正 | `status` = `MATCHING`・`InProgress`・`unknown`（Theory） | 422。`details` = `[{"field":"status","message":"予約状態の値が不正です"}]` | 高 |
| H-072 | `ListAsync` | 状態が空 | `status=`・`status=%20` | 200。`LastListFilter.Status` = `null` | 中 |
| H-073 | `ListAsync` | page / limit の受け渡し | `page=0&limit=0` | 200。`meta.page` = 0、`meta.limit` = 0、`LastListFilter` も 0 / 0（補正はリポジトリの責務） | 中 |
| H-074 | `ListAsync` | クエリの形式 | `date=abc`・`from=abc`・`to=abc`・`page=abc`・`limit=abc`（Theory） | 400 | 中 |

### 4.10 GET /api/reservations/{reservationId}（`ReservationHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-075 | `GetAsync` | 正常 | 本人の予約 | 200。`data` のキーは H-055 と同じ。値が Fake の予約と一致 | 高 |
| H-076 | `GetAsync` | 存在しない | Fake にない GUID | 404。`error.code` = `NOT_FOUND`、`error.message` = `指定された予約が見つかりません`、`details` キーなし | 高 |
| H-077 | `GetAsync` | 他人の予約 | 他人の予約の ID | 404。H-076 と同じボディ | 高 |
| H-078 | `GetAsync` | ID が GUID でない | `/api/reservations/abc` | 404。本文なし（ルートに一致しない） | 中 |
| H-079 | `GetAsync` | キャンセル済み | キャンセル済みの予約 | 200。`status` = `cancelled`、`cancellation_reason` が入り、`cancelled_at` は末尾 `Z` | 中 |

### 4.11 POST /api/reservations/{reservationId}/cancel（`ReservationHandlerTests`）

| ID | 対象 | 観点 | 前提・入力 | 期待結果（ステータス・ボディ） | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-080 | `CancelAsync` | 正常（matching） | 本人の `matching` の予約、`{"reason":"予定が変わったため"}` | 200。`data` のキーは `id` / `status` / `cancellation_reason` / `cancelled_at` のみ。`status` = `cancelled`、`cancellation_reason` = 送った理由、`cancelled_at` は末尾 `Z`。Fake が更新されている | 高 |
| H-081 | `CancelAsync` | 正常（confirmed） | 本人の `confirmed` の予約 | 200。`status` = `cancelled` | 高 |
| H-082 | `CancelAsync` | 理由なし | `{}` | 200。`cancellation_reason` = `null`（キーあり） | 中 |
| H-083 | `CancelAsync` | キャンセルできない状態 | `in_progress` / `completed` / `cancelled` の予約（Theory） | 409。`CONFLICT`、`error.message` = `Cannot cancel a reservation in status InProgress.` など（domain の例外メッセージそのまま）、`details` キーなし | 高 |
| H-084 | `CancelAsync` | 存在しない・他人の予約 | Fake にない GUID／他人の予約の ID（Theory） | 404。`NOT_FOUND`、`指定された予約が見つかりません`。他人の予約は変更されていない | 高 |
| H-085 | `CancelAsync` | ボディなし | ボディなし | 400 | 中 |
| H-086 | `CancelAsync` | ID が GUID でない | `/api/reservations/abc/cancel` | 404。本文なし | 低 |

### 4.12 CurrentUser（`CurrentUserTests`）

HTTP を使わず、`ClaimsPrincipal` を直接作って確認します。

| ID | 対象 | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| H-087 | `FindSubject` | `sub` あり | `sub` クレームを持つ `ClaimsPrincipal` | `sub` の値を返す | 中 |
| H-088 | `FindSubject` | `sub` なし | クレームなし | `null` | 中 |
| H-089 | `FindSubject` | 変換後のクレーム名 | `ClaimTypes.NameIdentifier` だけを持つ | `null`（`MapInboundClaims = false` の前提で `sub` だけを見る） | 低 |

## 5. 既知の問題・仕様の曖昧な点

実装を読んで気づいた点です。確認していないものは「要確認」としています。

| # | 内容 | テストでの扱い |
| --- | --- | --- |
| 1 | `requested_pickup_at` をオフセット付き（`+09:00`）で送ると、System.Text.Json は `DateTimeKind.Local` の値に変換する。201 のレスポンスは保存前のオブジェクトをそのまま返すため、`Z` ではなくサーバーのタイムゾーンのオフセット付きで返る可能性がある（ENDPOINT.md は「レスポンスの日時は UTC（末尾が `Z`）」）。オフセットなしで送った場合も `Z` なしになる可能性がある。**確認済み**：`+09:00` で送るとレスポンスは `+09:00`（実行環境のオフセット）、オフセットなしで送ると `2026-10-01T09:00:00`（`Z` なし）で返る。ENDPOINT.md と食い違う | H-056 は `Z` 付きの入力で確認する。オフセット付きの入力は、結果が実行環境（ローカルは JST、CI は UTC）で変わるおそれがあるため、仕様が決まるまでケースに入れない |
| 2 | `requested_pickup_at` は仕様上必須だが、省略すると `0001-01-01T00:00:00` として受け付けられ、201 になる（確認済み。レスポンスも `Z` なし）。過去の日時のチェックもない | 現状の動作を固定するケースは作らない。仕様を決めてから追加する |
| 3 | ボディの読み取り（400）はハンドラーの本人確認（401）より先に行われる。ユーザー不在のトークンで不正なボディを送ると 400 になる | 401 のテストでは正しいボディを送る |
| 4 | 401 には 2 種類ある。認証ミドルウェアが返すもの（`WWW-Authenticate: Bearer` あり）と、ハンドラーの `Results.Unauthorized()`（`sub` なし・ユーザー不在。`WWW-Authenticate` なし）。ENDPOINT.md では区別していない | `WWW-Authenticate` は H-001 だけで確認する |
| 5 | `POST /api/reservations` と `POST /api/users/me/roles` は `InvalidOperationException` をすべて捕まえ、固定のメッセージの 409 にしている。想定外の `InvalidOperationException` も 409 になる | Fake からは起きないため、ケースにしない |
| 6 | `UserNotFoundException`（usecase）はどこでも捕まえていない。本人確認のあとでユーザーが消えた場合などは 500 になる | 対象外（6 章） |
| 7 | `details[].field` は `role` / `password` / `status` 以外がキャメルケース（ENDPOINT.md の「既知の制約」どおり）。`details[].message` も英語 | 現状の値（`kanaLastName` など）で確認する。修正したらテストも直す |
| 8 | 409 のキャンセル不可のメッセージは domain の例外メッセージそのもの | H-083 は完全一致で確認する。domain の文言を変えると handler のテストも落ちる |
| 9 | `meta.page` / `meta.limit` はリクエストの値をそのまま返す。ENDPOINT.md の「1 未満は 1 / 50 として扱う」はリポジトリでの補正 | handler 層では生の値の受け渡し（H-073）だけを確認する |
| 10 | 最小 API の JSON 既定値（`JsonSerializerDefaults.Web`）では数値を文字列でも読めるため、`"passenger_count": "2"` は 400 にならず受け付けられると考えられる。**確認済み**：201 になり `passenger_count` = 2 として保存される | 400 のケースは `"abc"` のように数値として読めない値を使う |
| 11 | `HealthCheckMiddleware` は既定で `Cache-Control` などのキャッシュ禁止ヘッダーを付け、そのあと `WriteResponseAsync` が `Cache-Control: no-store` で上書きすると考えられる。`Pragma` などが残る可能性がある。**確認済み**：`Cache-Control: no-store` に上書きされ、`Pragma: no-cache` と `Expires: Thu, 01 Jan 1970 00:00:00 GMT` は残る | `Cache-Control` は文字列の完全一致ではなく `NoStore` が `true` かで確認する |
| 12 | `/health` の Degraded は 200 になる（ASP.NET Core の既定）。ENDPOINT.md は 200 / 503 しか書いていない（SWAGGER.yaml の列挙には `Degraded` がある） | H-018 で現状を確認する。今のチェックは Degraded を返さない |
| 13 | クエリの `from` / `to` にオフセット付きの日時を入れる場合、`+` を `%2B` にしないと空白として解釈される。バインド後の `DateTime` の `Kind` は **確認済み**：`Utc`（`Z` 付きはそのまま、`%2B09:00` 付きは UTC に変換される） | H-068 は `Z` 付きの値を使い、比較は時刻の値（UTC に揃えて）で行う |
| 14 | Development 環境では最小 API のバインドエラーが例外として投げられ、開発者例外ページが応答する（400 の本文が変わる） | テストは `Testing` 環境で実行し、400 はステータスだけを確認する |
| 15 | メールアドレスの重複判定の大文字小文字の扱いは、本番は SQL Server の照合順序に、テストは Fake の実装に依存する | H-032 は完全に同じ文字列で確認する |

## 6. 対象外

| 内容 | 理由 |
| --- | --- |
| 文字数の上限超過（姓名 50、乗車地 200 など）で 500 になること | API ではチェックしておらず DB の制約で起きるため。Fake では再現できない（infrastructure 層、または結合テスト） |
| 日付の日本時間での絞り込み、ページングの補正、並び順、`limit` の上限なし | リポジトリの責務（infrastructure 層） |
| domain の検証ルールの網羅（カタカナの判定、パスワードの文字数の境界など） | domain 層・usecase 層で確認する。handler 層は代表値で 422 と `field` への変換だけを見る |
| `JwtOptions.Validate()` による起動時の設定チェック | infrastructure 層で `JwtOptions` を直接テストする |
| `JwtAccessTokenIssuer`、`AspNetPasswordHasher`、`DatabaseHealthCheck` 自体の動作 | infrastructure 層で確認する（handler 層では本物または Fake として使うだけ） |
| 未定義の列挙値での 500（`ToStatusString` / `ToRoleString` の `ArgumentOutOfRangeException`） | domain の列挙値がすべて変換対象になっており、通常の操作では起きない |
| `UserNotFoundException` による 500（本人確認後にユーザーが消えた場合） | 同時実行のタイミングでしか起きない |
| CORS、OpenAPI（`/openapi/v1.json`） | Development 環境だけの設定で、テストは `Testing` 環境で動かす |
| HTTPS リダイレクト | 環境（HTTPS ポートの設定）に依存し、API の仕様ではない |
| 実際の DB を使った結合テスト、性能・負荷 | 単体テストの範囲外 |
