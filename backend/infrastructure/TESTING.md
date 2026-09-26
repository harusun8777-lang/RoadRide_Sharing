# infrastructure 層 単体テスト設計書

`backend/infrastructure/` に対する単体テストの設計です。
domain / usecase / handler 層のテスト設計書と同じルールで書いています。

> infrastructure のコード（特に `AppDbContext` のマッピング）を変更したら、このファイルのテストケースも見直してください。

## 目次

1. [目的と範囲](#1-目的と範囲)
2. [テスト方針](#2-テスト方針)
3. [テスト対象一覧](#3-テスト対象一覧)
4. [テストケース](#4-テストケース)
5. [既知の問題・仕様の曖昧な点](#5-既知の問題仕様の曖昧な点)
6. [対象外](#6-対象外)

## 1. 目的と範囲

### 目的

- usecase のリポジトリインターフェースの実装が、SQL Server 上で仕様どおりに保存・検索できることを確かめる
- EF Core のマッピング（テーブル名・列名・最大長・必須・一意制約・外部キー・値変換・UTC 変換）が `docs/DATA_MODEL.md` と実装の意図どおりであることを確かめる
- 保存して別の `DbContext` で読み直したとき、ドメインオブジェクト（`User` / `Rider` / `Driver` / `Reservation` / `RideGroup`）が元どおりに復元されることを確かめる
- JWT の発行・検証設定とパスワードハッシュが、認証の安全性を損なわないことを確かめる
- DB のヘルスチェックが、接続できないときに詳細を漏らさず Unhealthy を返すことを確かめる

### 範囲

| ファイル | 内容 |
| --- | --- |
| `AppDbContext.cs` | EF Core のモデル定義、値変換（区分・状態の文字列化、DateTime の UTC 化） |
| `Auth.cs` | `JwtOptions`（設定値と検証パラメータ）、`JwtAccessTokenIssuer`（`IAccessTokenIssuer`）、`AspNetPasswordHasher`（`IPasswordHasher`） |
| `EfUserRepository.cs` | `IUserRepository` の実装 |
| `EfReservationRepository.cs` | `IReservationRepository` の実装（一覧のフィルタ・ページング・並び順、未完了判定） |
| `EfRideGroupRepository.cs` | `IRideGroupRepository` の実装（運転手の未完了便の判定） |
| `DatabaseHealthCheck.cs` | `/health` から使う DB 接続確認 |

## 2. テスト方針

### ツール

| 項目 | 内容 |
| --- | --- |
| テストプロジェクト | `tests/backend.Tests/`（`backend/` の中に置くと `backend.csproj` が `**/*.cs` を取り込むため外に置く） |
| フォルダ | 層ごとに `Domain/`, `Usecase/`, `Handler/`, `Infrastructure/`。この層は `tests/backend.Tests/Infrastructure/`、共通のフィクスチャは `tests/backend.Tests/Infrastructure/Fixtures/` |
| フレームワーク | xUnit |
| アサーション | xUnit 標準の `Assert`（FluentAssertions は v8 から商用ライセンスのため使わない） |
| DB | Testcontainers（`Testcontainers.MsSql`）で SQL Server 2022 のコンテナを起動する。イメージは `docker-compose.yml` と同じ `mcr.microsoft.com/mssql/server:2022-latest` |
| モックライブラリ | 使わない |
| テストクラス名 | `{対象クラス}Tests`（例：`EfReservationRepositoryTests`） |
| テストメソッド名 | `{メソッド}_{条件}_{期待結果}`（日本語可。例：`ListAsync_日本時間の暦日を指定_JSTの0時から24時の予約だけ返る`） |
| テストケース ID | `I-001` から連番 |
| 優先度 | 高 / 中 / 低 |

### DB を使うテストと使わないテスト

| 区分 | 対象 | DB |
| --- | --- | --- |
| 純粋な単体テスト | `JwtOptions`、`JwtAccessTokenIssuer`、`AspNetPasswordHasher`、`AppDbContext` のモデル定義と値変換（`context.Model` のメタデータを読むだけ） | 不要。`UseSqlServer("Server=unused")` で作った `DbContext` はモデル構築だけなら接続しない |
| DB テスト | `AppDbContext` の DB 上の挙動、`Ef*Repository`、`DatabaseHealthCheck` | Testcontainers の SQL Server |

EF Core InMemory や SQLite は使いません。照合順序（大文字小文字を区別しない比較）、`nvarchar(n)` の最大長、一意インデックス、CHECK 制約、`datetime2` の精度、`uniqueidentifier` の並び順など、SQL Server 固有の挙動をそのまま確かめるためです。

### カテゴリ（Trait）

DB テストは Docker が必要なので、クラスに `[Trait("Category", "Database")]` を付けて通常のテストと分けます。

| 実行するもの | コマンド |
| --- | --- |
| DB 以外（Docker 不要。普段の開発・PR の必須チェック） | `dotnet test tests/backend.Tests --filter "Category!=Database"` |
| DB テストだけ | `dotnet test tests/backend.Tests --filter "Category=Database"` |

- 上のフィルタは VSTest の書式です。Microsoft Testing Platform で動かす場合は `--filter-not-trait "Category=Database"` などに読み替えます
- GitHub Actions の `ubuntu-latest` には Docker があるため、CI では DB テストも別ジョブで実行します

### データの分離

| 方針 | 内容 |
| --- | --- |
| コンテナ | テスト実行全体で1つだけ起動する（`SqlServerFixture` を `ICollectionFixture` として共有）。起動に数十秒かかるため |
| DB | **テストごとに新しい DB を作る**。テストクラスの `IAsyncLifetime.InitializeAsync` で `test_{Guid:N}` という名前の DB を `EnsureCreatedAsync` で作り、`DisposeAsync` で `EnsureDeletedAsync` する。本番（`Program.cs`）も `EnsureCreated` なので同じスキーマになる |
| 読み直し | 保存に使った `DbContext` とは別の `DbContext` を作って読む。同じコンテキストだと追跡中のインスタンスがそのまま返り、復元を確かめられないため |
| 生 SQL | 列に入った文字列や UTC 値を直接確かめるときは `context.Database.SqlQuery<T>($"...")` を使う |

トランザクションのロールバックによる分離は使いません。理由は次のとおりです。

- 読み直し用の別コンテキストで同じ接続・トランザクションを共有させる必要があり、テストが複雑になる
- 一意制約違反などのエラーを起こすテストでトランザクションの状態が扱いにくい
- 本番と同じ `EnableRetryOnFailure` を付けると、利用者が開始したトランザクションが使えない

DB 作成が遅くテスト時間が問題になったら、DB をクラス単位で共有し Respawn などで各テスト前に空にする方式を検討します。

テスト用の `DbContext` は `EnableRetryOnFailure` を付けずに作ります（失敗をすぐ検出するため）。

### テストダブルの方針

- DB は本物（コンテナ）を使い、リポジトリや `DbContext` のモックは作らない
- `DatabaseHealthCheck` のロガーは、呼び出しを記録する手書きの `RecordingLogger<T>`（`ILogger<T>` 実装）を使う
- `JwtAccessTokenIssuer` には `Options.Create(new JwtOptions { ... })` で設定を渡す
- テストデータのユーザーは `User.Create`、予約は `Reservation.Create`、便は `RideGroup.Propose` で作る。`Rider.Create` / `Driver.Create` は `internal` なので `User.Create` / `AddRole` 経由で作る
- `RideGroup` には状態を変えるメソッドもリポジトリの追加メソッドもないため、便は `AppDbContext.RideGroups.Add` で保存し、状態は `ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, ...))` で変える

### 時刻の検証

- `JwtAccessTokenIssuer.Issue` は `DateTime.UtcNow` を直接使うため時刻を固定できない。呼び出し前後の `UtcNow` で挟み、その範囲に入ることを確かめる
- DB の DateTime は、保存前の値と `Ticks` と `Kind` の両方を比べる
- `DateTimeKind.Local` の変換結果はホストのタイムゾーンに依存する。期待値は `value.ToUniversalTime()` で求め、固定値を書かない（[5章](#5-既知の問題仕様の曖昧な点) 参照）

### 例外の検証

- DB 制約違反は `Assert.ThrowsAsync<DbUpdateException>` で受け、`InnerException` の `SqlException.Number` まで確かめる（一意インデックス違反 `2601`、外部キー・CHECK 制約違反 `547`、文字列の切り捨て `2628`）
- `DbUpdateConcurrencyException` は `DbUpdateException` の派生なので、区別したいときは `Assert.ThrowsAsync`（完全一致）を使う

### 書き方の例

```csharp
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Database")]
public class EfReservationRepositoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private string _connectionString = "";

    public async ValueTask InitializeAsync()
    {
        _connectionString = fixture.CreateDatabaseConnectionString(); // test_{Guid:N}
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options);

    [Fact]
    public async Task ListAsync_日本時間の暦日を指定_JSTの0時ちょうどは含まれる()
    {
        var rider = await SeedRiderAsync();
        var jstMidnight = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc); // 2026-10-01 00:00 JST
        await SeedReservationAsync(rider.Id, jstMidnight);

        await using var db = CreateContext(); // 保存とは別のコンテキストで読む
        var (items, total) = await new EfReservationRepository(db)
            .ListAsync(new ReservationListFilter(Date: new DateOnly(2026, 10, 1)));

        Assert.Equal(1, total);
        Assert.Equal(jstMidnight, Assert.Single(items).RequestedPickupAt);
    }
}
```

（xUnit v2 では `InitializeAsync` / `DisposeAsync` の戻り値が `Task` になります。）

### この層で書かないこと

- HTTP ステータスへの変換、レスポンスの `meta` の値（handler 層）
- ユースケースの呼び出し順や、`EmailAlreadyRegisteredException` などの業務例外の送出（usecase 層）
- ドメインの状態遷移ルール（domain 層）。ここでは遷移後の状態が保存・復元されることだけを確かめる
- DI 登録と JwtBearer ミドルウェアの設定（`Program.cs`。結合テストの範囲）

## 3. テスト対象一覧

| クラス | メソッド・対象 | 実装するインターフェース | テストクラス | DB |
| --- | --- | --- | --- | --- |
| `JwtOptions` | `Validate`、`CreateSigningKey`、`CreateValidationParameters`、既定値 | － | `JwtOptionsTests` | 不要 |
| `JwtAccessTokenIssuer` | `Issue` | `IAccessTokenIssuer` | `JwtAccessTokenIssuerTests` | 不要 |
| `AspNetPasswordHasher` | `Hash`、`Verify` | `IPasswordHasher` | `AspNetPasswordHasherTests` | 不要 |
| `AppDbContext` | `OnModelCreating` のモデル定義、値変換 | － | `AppDbContextTests.Model`（入れ子クラス） | 不要 |
| `AppDbContext` | スキーマ作成、値変換・UTC 変換・制約の DB 上の挙動 | － | `AppDbContextTests.Database`（入れ子クラス） | 必要 |
| `EfUserRepository` | `FindByIdAsync`、`FindByEmailAsync`、`AddAsync`、`UpdateAsync` | `IUserRepository` | `EfUserRepositoryTests` | 必要 |
| `EfReservationRepository` | `FindByIdAsync`、`ListAsync`、`HasUnfinishedAsync`、`AddAsync`、`UpdateAsync` | `IReservationRepository` | `EfReservationRepositoryTests` | 必要 |
| `EfRideGroupRepository` | `HasUnfinishedByDriverAsync` | `IRideGroupRepository` | `EfRideGroupRepositoryTests` | 必要 |
| `DatabaseHealthCheck` | `CheckHealthAsync` | `IHealthCheck` | `DatabaseHealthCheckTests` | 必要 |

`AppDbContext` は DB の要否でトレイトとフィクスチャが変わるため、クラス名の規則を保ったまま `AppDbContextTests` の入れ子クラス `Model` / `Database` に分けます。

## 4. テストケース

マッピングの期待値（`AppDbContext.cs` の定義）:

| テーブル | 列（最大長） | 一意 | 外部キー |
| --- | --- | --- | --- |
| `users` | `id`、`email`(254)、`password_hash`(255)、`last_name`(50)、`first_name`(50)、`kana_last_name`(50)、`kana_first_name`(50)、`active_role`(20)、`created_at` | `email` | － |
| `riders` | `user_id`、`created_at` | － | `user_id` → `users.id`（Cascade） |
| `drivers` | `user_id`、`created_at` | － | `user_id` → `users.id`（Cascade） |
| `ride_groups` | `id`、`group_number`(32)、`driver_id`、`status`(20)、`created_at`、`updated_at` | `group_number` | `driver_id` → `drivers.user_id`（Restrict） |
| `reservations` | `id`、`reservation_number`(32)、`user_id`、`pickup_location`(200)、`destination`(200)、`requested_pickup_at`、`passenger_count`、`consideration_notes`(500)、`status`(20)、`cancellation_reason`(500)、`cancelled_at`、`created_at`、`updated_at` | `reservation_number` | `user_id` → `riders.user_id`（Restrict） |

任意（NULL 可）は `consideration_notes`、`cancellation_reason`、`cancelled_at` だけです。`reservations.requested_pickup_at` には一意でないインデックス、`passenger_count` には CHECK 制約 `CK_reservations_passenger_count`（`>= 1`）があります。

### 4.1 JwtOptions（JwtOptionsTests）

正常な設定は「Issuer・Audience が `RoadRideSharing`、SigningKey が ASCII 32 文字、ExpiresMinutes が 60」とします。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-001 | Validate | 正常 | 正常な設定 | 例外にならない | 高 |
| I-002 | Validate | Issuer 未設定 | Issuer が `""` / `"   "`（Theory） | `InvalidOperationException`（メッセージに `Jwt:Issuer`） | 高 |
| I-003 | Validate | Audience 未設定 | Audience が `""` / `"   "`（Theory） | `InvalidOperationException` | 高 |
| I-004 | Validate | 鍵の長さの境界 | SigningKey が ASCII 31 文字 | `InvalidOperationException`（メッセージに `32 bytes`） | 高 |
| I-005 | Validate | 鍵の長さの境界 | SigningKey が ASCII 32 文字 | 例外にならない | 高 |
| I-006 | Validate | 文字数ではなく UTF-8 のバイト数で判定 | SigningKey が `あ`×10（30 バイト）/ `あ`×11（33 バイト） | 10 文字は例外、11 文字は通る | 中 |
| I-007 | Validate | 有効期限 | ExpiresMinutes が `0` / `-1`（Theory） | `InvalidOperationException` | 高 |
| I-008 | Validate | 有効期限の境界 | ExpiresMinutes が `1` | 例外にならない | 低 |
| I-009 | 既定値 | 既定値 | `new JwtOptions()` | `SectionName` が `"Jwt"`、`ExpiresMinutes` が 60、Issuer・Audience・SigningKey が空。`Validate()` は例外になる（設定漏れで起動が止まる） | 中 |
| I-010 | CreateSigningKey | 鍵の中身 | 正常な設定 | `SymmetricSecurityKey.Key` が `Encoding.UTF8.GetBytes(SigningKey)` と一致 | 中 |
| I-011 | CreateValidationParameters | 検証パラメータ | 正常な設定 | `ValidIssuer` / `ValidAudience` が設定値、`ValidAlgorithms` が `[HS256]` のみ、`ClockSkew` が 30 秒、`IssuerSigningKey` の鍵が I-010 と同じ | 高 |
| I-012 | CreateValidationParameters | 検証が無効化されていない | 正常な設定 | `ValidateIssuer` / `ValidateAudience` / `ValidateLifetime` / `RequireSignedTokens` / `RequireExpirationTime` が true | 中 |
| I-013 | Validate | 空白だけの鍵（現状の挙動） | SigningKey が半角空白 32 文字 | 例外にならない（[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |

### 4.2 JwtAccessTokenIssuer（JwtAccessTokenIssuerTests）

発行したトークンは `JsonWebTokenHandler.ValidateTokenAsync(token, options.CreateValidationParameters())` で検証します。検証失敗のケースは、テスト内で `JsonWebTokenHandler.CreateToken` を使って条件を変えたトークンを作ります。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-014 | Issue | 自身の検証設定で検証できる | 正常な設定、`User.Create` で作ったユーザー | `IsValid` が true | 高 |
| I-015 | Issue | sub | 同上 | `sub` クレームが `user.Id.ToString()`（`GetCurrentUserUseCase` が `Guid.TryParse` できる形式） | 高 |
| I-016 | Issue | iss / aud | 同上 | `iss` / `aud` が設定値 | 高 |
| I-017 | Issue | ヘッダー | 同上 | `alg` が `HS256`、`typ` が `JWT` | 中 |
| I-018 | Issue | 有効期限 | ExpiresMinutes = 60 | `ExpiresAt` が「呼び出し前の UtcNow + 60 分」以上「呼び出し後の UtcNow + 60 分」以下、`Kind` が `Utc` | 高 |
| I-019 | Issue | 設定の反映 | ExpiresMinutes = 5 | `ExpiresAt` が約 5 分後 | 中 |
| I-020 | Issue | `exp` と `ExpiresAt` の一致 | 同上 | `exp`（秒単位）と `ExpiresAt` の差が 1 秒未満 | 中 |
| I-021 | Issue | 個人情報を含めない | 同上 | ペイロードのクレームが `sub` / `iss` / `aud` / `exp` / `iat` / `nbf` だけ。メールアドレス・氏名・区分を含まない | 中 |
| I-022 | 検証設定 | 改ざん | 発行したトークンのペイロードの `sub` を書き換え、署名はそのまま | 検証失敗 | 高 |
| I-023 | 検証設定 | 別の鍵 | 別の 32 バイト鍵で署名したトークン | 検証失敗 | 高 |
| I-024 | 検証設定 | 発行者違い | Issuer が別の値のトークン | 検証失敗 | 高 |
| I-025 | 検証設定 | 対象者違い | Audience が別の値のトークン | 検証失敗 | 高 |
| I-026 | 検証設定 | アルゴリズムの制限 | 同じ鍵で HS512 署名したトークン | 検証失敗（`ValidAlgorithms` が HS256 のみ） | 中 |
| I-027 | 検証設定 | 署名なし | `alg: none` の未署名トークン | 検証失敗 | 高 |
| I-028 | 検証設定 | 時刻のずれの許容 | `exp` が 20 秒前 / 40 秒前（`nbf` / `iat` は十分過去） | 20 秒前は成功、40 秒前は失敗（ClockSkew 30 秒） | 中 |
| I-029 | Issue | 不正な設定で発行（`Validate` を通していない） | SigningKey が 16 バイト | 例外になる（HS256 の鍵長不足）。`Validate` が起動時に止めている前提を確かめる | 低 |

### 4.3 AspNetPasswordHasher（AspNetPasswordHasherTests）

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-030 | Hash | 平文を保存しない | `"password123"` | 空でなく、平文を含まない | 高 |
| I-031 | Hash | ソルト | 同じパスワードを 2 回 | 2 つのハッシュが異なる | 高 |
| I-032 | Hash | 列に収まる | 長さ 128（`RegisterUserUseCase.MaxPasswordLength`）のパスワード | 長さが 255（`password_hash` の最大長）以下 | 中 |
| I-033 | Hash | 形式 | `"password123"` | Base64 でデコードでき、先頭バイトが `0x01`（Identity V3 形式、PBKDF2） | 中 |
| I-034 | Verify | 正しいパスワード | `Hash("password123")` と `"password123"` | true | 高 |
| I-035 | Verify | 違うパスワード | `Hash("password123")` と `"password124"` | false | 高 |
| I-036 | Verify | 大文字小文字の区別 | `Hash("Password123")` と `"password123"` | false | 中 |
| I-037 | Verify | 非 ASCII | `"パスワード🔑abc"` をハッシュして同じ値で検証 | true | 中 |
| I-038 | Verify | 別インスタンス（シングルトン登録・再起動後を想定） | インスタンス A で Hash、B で Verify | true | 中 |
| I-039 | Verify | 空のハッシュ | `Verify("", "password123")` | false | 中 |
| I-040 | Verify | 再ハッシュが必要な形式 | Identity V2 形式（`PasswordHasherCompatibilityMode.IdentityV2` の `PasswordHasher` で作ったハッシュ）と正しいパスワード | true（`SuccessRehashNeeded` も成功扱い） | 低 |
| I-041 | Verify | 不正な形式のハッシュ | Base64 でない文字列（例：`"not-a-hash"`） | 現状の挙動（例外か false か）を確かめて固定する（[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |

### 4.4 AppDbContext のモデル定義（AppDbContextTests.Model、DB 不要）

`context.Model.FindEntityType(typeof(...))` のメタデータを読みます。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-042 | OnModelCreating | テーブル名 | 5 エンティティ | `users` / `riders` / `drivers` / `ride_groups` / `reservations` | 高 |
| I-043 | OnModelCreating | 列名 | 全プロパティ（Theory） | [4章冒頭の表](#4-テストケース) のスネークケースの列名 | 高 |
| I-044 | OnModelCreating | 最大長 | 文字列プロパティ（Theory） | 表の最大長と一致 | 高 |
| I-045 | OnModelCreating | 必須・任意 | 全プロパティ | `ConsiderationNotes` / `CancellationReason` / `CancelledAt` だけ NULL 可 | 中 |
| I-046 | OnModelCreating | インデックス | － | `Email` / `GroupNumber` / `ReservationNumber` が一意、`RequestedPickupAt` が一意でないインデックス | 高 |
| I-047 | OnModelCreating | 主キーの生成 | － | `User.Id`、`Rider.UserId`、`Driver.UserId`、`RideGroup.Id`、`Reservation.Id` が `ValueGenerated.Never` | 中 |
| I-048 | OnModelCreating | 計算プロパティを保存しない | － | `FullName` / `KanaFullName` / `Roles` がマップされていない | 中 |
| I-049 | OnModelCreating | 外部キーと削除動作 | － | 表の外部キー先と削除動作（Cascade / Restrict）と一致 | 中 |
| I-050 | OnModelCreating | CHECK 制約 | － | `reservations` に `CK_reservations_passenger_count`（`[passenger_count] >= 1`） | 中 |
| I-051 | 値変換（UserRole） | 双方向 | `Rider` / `Driver`、`"rider"` / `"driver"` | `"rider"` / `"driver"` に変換、逆変換で元に戻る | 高 |
| I-052 | 値変換（UserRole） | 不明な文字列 | `"admin"`、`"Driver"`（大文字） | どちらも `Rider` になる（現状の挙動。[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |
| I-053 | 値変換（ReservationStatus） | 双方向 | 5 値（Theory） | `matching` / `confirmed` / `in_progress` / `completed` / `cancelled` と相互に変換できる | 高 |
| I-054 | 値変換（RideGroupStatus） | 双方向 | 5 値（Theory） | `proposed` / `confirmed` / `in_progress` / `completed` / `cancelled` と相互に変換できる | 高 |
| I-055 | 値変換（状態） | 不明な値 | 文字列 `"unknown"`、範囲外の enum 値 `(ReservationStatus)99`（予約・便の両方） | `ArgumentOutOfRangeException` | 中 |
| I-056 | 値変換（DateTime） | UTC 化 | `Kind` が `Utc` / `Local` / `Unspecified` の値を `ConvertToProvider` | `Local` だけ `ToUniversalTime()` され、他はそのまま | 高 |
| I-057 | 値変換（DateTime） | 読み出し | 任意の値を `ConvertFromProvider` | 値は変わらず `Kind` が `Utc` | 高 |
| I-058 | 値変換（DateTime?） | null | `null` を双方向に変換 | `null` のまま。値があるときは I-056 / I-057 と同じ | 中 |

### 4.5 AppDbContext の DB 上の挙動（AppDbContextTests.Database）

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-059 | EnsureCreated | スキーマ作成 | 空の DB | 5 テーブルと一意インデックス・CHECK 制約が作られる（`INFORMATION_SCHEMA` / `sys.indexes` で確認） | 高 |
| I-060 | 前提の確認 | 照合順序 | テスト用 DB | `DATABASEPROPERTYEX(DB_NAME(), 'Collation')` が大文字小文字を区別しない照合順序（`_CI_`）。以降のメール検索のテストの前提 | 中 |
| I-061 | 値変換 | 状態・区分が文字列で入る | Rider のユーザーと Matching の予約を保存 | 生 SQL で `users.active_role` が `rider`、`reservations.status` が `matching` | 高 |
| I-062 | UTC 変換 | Utc の往復 | `Kind = Utc` の `RequestedPickupAt` で予約を保存し、別コンテキストで読む | `Ticks` が一致、`Kind` が `Utc` | 高 |
| I-063 | UTC 変換 | Local の保存 | `Kind = Local` の値で保存 | 生 SQL の値が `value.ToUniversalTime()` と一致、読み出すと `Kind` が `Utc` | 高 |
| I-064 | UTC 変換 | Unspecified の保存（現状の挙動） | `Kind = Unspecified` の値で保存 | 値はそのまま保存され、読み出すと同じ値で `Kind` が `Utc`（UTC とみなされる） | 中 |
| I-065 | UTC 変換 | 精度 | `Ticks` の下 7 桁が 0 でない値（例：`...1234567` 100ns 単位） | `datetime2` で丸められず `Ticks` が一致 | 中 |
| I-066 | UTC 変換 | 全 DateTime 列 | ユーザー（`created_at`、riders / drivers の `created_at`）、予約（`created_at` / `updated_at` / `cancelled_at`）、便（`created_at` / `updated_at`）を保存して読む | すべて `Kind` が `Utc` | 中 |
| I-067 | 最大長 | 境界ちょうど | 各文字列列に最大長ちょうどの値（Theory。姓名・読み仮名は `ア` の繰り返し、メールは `a...@example.com` で 254 文字） | 保存でき、読み直して一致 | 高 |
| I-068 | 最大長 | 1 文字超過 | 各文字列列に最大長 + 1 の値（Theory） | `DbUpdateException`（内部の `SqlException.Number` が `2628`） | 高 |
| I-069 | 最大長 | 日本語 | 乗車地に日本語 200 文字 | 保存でき、文字化けせず読み直せる（`nvarchar`） | 中 |
| I-070 | 最大長 | サロゲートペア | 姓に `𠮷` を 25 文字（UTF-16 で 50）/ 26 文字 | 25 文字は保存でき、26 文字は `DbUpdateException`（[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |
| I-071 | CHECK 制約 | 乗車人数 0 | 生 SQL で `passenger_count = 0` の予約を INSERT（ドメインでは作れないため） | `SqlException.Number` が `547` | 中 |
| I-072 | 値変換 | DB に不明な状態 | 生 SQL で `reservations.status` を `'unknown'` に更新して読む | 例外になる（`ArgumentOutOfRangeException`、または EF Core がそれを包んだ例外） | 低 |
| I-073 | 値変換 | DB に不明な区分（現状の挙動） | 生 SQL で `users.active_role` を `'admin'` に更新して読む | 例外にならず `ActiveRole` が `Rider` | 低 |
| I-074 | 一意制約 | 便番号の重複 | 同じ `GroupNumber` の便を 2 件保存 | `DbUpdateException`（`2601`） | 低 |
| I-075 | 外部キー | 運転手でないユーザーの便 | Rider だけのユーザーを `DriverId` にした便を保存 | `DbUpdateException`（`547`） | 低 |

### 4.6 EfUserRepository（EfUserRepositoryTests）

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-076 | AddAsync | Rider で登録 | `User.Create(..., UserRole.Rider)` | `users` と `riders` に 1 行ずつ、`drivers` は 0 行 | 高 |
| I-077 | AddAsync | Driver で登録 | `User.Create(..., UserRole.Driver)` | `users` と `drivers` に 1 行ずつ、`riders` は 0 行 | 中 |
| I-078 | FindByIdAsync | 保存と再読み込み | I-076 のユーザーを別コンテキストで取得 | `Id` / `Email` / `PasswordHash` / 姓名 / 読み仮名 / `ActiveRole` / `CreatedAt`（`Kind` が `Utc`）が一致、`Rider` あり・`Driver` なし、`Roles` が `[Rider]`、`FullName` が `"姓 名"` | 高 |
| I-079 | FindByIdAsync | 両方のプロフィール | `AddRole(Driver)` 済みのユーザー | `Rider` と `Driver` の両方が読み込まれ、`HasRole` がどちらも true | 中 |
| I-080 | FindByIdAsync | 存在しない | 新しい `Guid` | null | 高 |
| I-081 | FindByEmailAsync | 一致 | 登録済みのメールアドレス | そのユーザー（`Rider` / `Driver` も読み込まれる） | 高 |
| I-082 | FindByEmailAsync | 存在しない | 未登録のメールアドレス | null | 高 |
| I-083 | FindByEmailAsync | 大文字小文字違い | `user@example.com` で登録し `USER@example.com` で検索 | 見つかる（照合順序が `_CI_` のため。I-060 が前提） | 中 |
| I-084 | FindByEmailAsync | 末尾の空白 | `user@example.com ` で検索 | 見つかる（SQL Server の文字列比較は末尾の空白を無視する） | 低 |
| I-085 | AddAsync | メールの重複 | 同じメールアドレスのユーザーを 2 人登録 | 2 人目で `DbUpdateException`（`2601`） | 高 |
| I-086 | AddAsync | 大文字小文字違いのメール | `user@example.com` と `USER@example.com` | 2 人目で `DbUpdateException`（`2601`） | 中 |
| I-087 | AddAsync | 失敗後の追跡状態 | I-085 の後、同じコンテキスト・リポジトリで別のメールのユーザーを登録 | 失敗したユーザーが追跡に残っているため再び失敗する（現状の挙動。[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |
| I-088 | UpdateAsync | 追跡中・区分の追加 | `FindByIdAsync` で取得（追跡中）→ `AddRole(Driver)` → `UpdateAsync` | `drivers` に行が追加され、別コンテキストで読むと `HasRole(Driver)` が true | 高 |
| I-089 | UpdateAsync | 追跡中・区分の切り替え | 両プロフィールを持つユーザーを取得 → `SwitchRole(Driver)` → `UpdateAsync` | 生 SQL で `active_role` が `driver`、読み直すと `ActiveRole` が `Driver` | 高 |
| I-090 | UpdateAsync | 切り離された状態・切り替え | コンテキスト A で取得した両プロフィールのユーザーを `SwitchRole` し、コンテキスト B のリポジトリで `UpdateAsync` | 保存される | 中 |
| I-091 | UpdateAsync | 切り離された状態・区分の追加 | コンテキスト A で取得した Rider だけのユーザーに `AddRole(Driver)` し、コンテキスト B で `UpdateAsync` | 現状の挙動を確かめて固定する（新しい `Driver` が更新扱いになり `DbUpdateConcurrencyException` になる可能性。[5章](#5-既知の問題仕様の曖昧な点) 参照） | 中 |

### 4.7 EfReservationRepository（EfReservationRepositoryTests）

予約の前提として、Rider のユーザーを `EfUserRepository.AddAsync` で保存しておきます。日時はすべて `DateTimeKind.Utc` で作ります（I-111 / I-112 を除く）。

#### AddAsync / FindByIdAsync / UpdateAsync

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-092 | AddAsync / FindByIdAsync | 保存と再読み込み | `Reservation.Create` の予約を保存し、別コンテキストで取得 | 全プロパティが一致。`Status` が `Matching`、`CancellationReason` / `CancelledAt` が null、日時の `Kind` が `Utc` | 高 |
| I-093 | AddAsync | 配慮事項なし | `considerationNotes` が null | 読み直しても null（空文字にならない） | 中 |
| I-094 | AddAsync | 予約番号の重複 | 同じ `ReservationNumber` の予約を 2 件 | 2 件目で `DbUpdateException`（`2601`） | 高 |
| I-095 | AddAsync | Rider プロフィールがない | Driver だけのユーザーの `UserId` | `DbUpdateException`（`547`） | 中 |
| I-096 | AddAsync | 存在しないユーザー | 新しい `Guid` の `UserId` | `DbUpdateException`（`547`） | 中 |
| I-097 | FindByIdAsync | 存在しない | 新しい `Guid` | null | 高 |
| I-098 | FindByIdAsync | 追跡される | 取得した予約 | `db.Entry(reservation).State` が `Unchanged`（`UpdateAsync` の追跡中の経路で使われる前提） | 中 |
| I-099 | UpdateAsync | 追跡中・キャンセル | `FindByIdAsync` で取得 → `Cancel("体調不良")` → `UpdateAsync` | 別コンテキストで読むと `Status` が `Cancelled`、理由・`CancelledAt`（`Kind` が `Utc`）・`UpdatedAt` が保存され、`CreatedAt` / `ReservationNumber` は変わらない | 高 |
| I-100 | UpdateAsync | 切り離された状態 | コンテキスト A で取得した予約を `Cancel` し、コンテキスト B のリポジトリで `UpdateAsync` | I-099 と同じ結果 | 中 |
| I-101 | UpdateAsync | 状態遷移の保存 | `Confirm` → `StartInProgress` → `Complete` をそれぞれ `UpdateAsync` | 各段階で読み直すと `confirmed` / `in_progress` / `completed` | 中 |
| I-102 | UpdateAsync | 存在しない予約 | 保存していない予約を切り離された状態で `UpdateAsync` | `DbUpdateConcurrencyException`（更新 0 行） | 低 |

#### HasUnfinishedAsync

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-103 | HasUnfinishedAsync | 未完了 | `Matching` / `Confirmed` / `InProgress` の予約が 1 件（Theory） | true | 高 |
| I-104 | HasUnfinishedAsync | 完了・キャンセルのみ | `Completed` と `Cancelled` の予約だけ | false | 高 |
| I-105 | HasUnfinishedAsync | 予約なし | 予約のない Rider | false | 高 |
| I-106 | HasUnfinishedAsync | 他人の予約 | 別ユーザーに `Matching` の予約、対象ユーザーは `Completed` だけ | false | 高 |
| I-107 | HasUnfinishedAsync | 混在 | `Completed` と `Confirmed` が 1 件ずつ | true | 中 |

#### ListAsync

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-108 | ListAsync | ユーザーで絞り込み | ユーザー A に 3 件、B に 2 件。`UserId = A` | A の 3 件だけ、`Total` が 3 | 高 |
| I-109 | ListAsync | ユーザー指定なし | 同上、`UserId = null` | 5 件、`Total` が 5 | 中 |
| I-110 | ListAsync | 状態で絞り込み | 5 つの状態の予約を 1 件ずつ、`Status` を各値（Theory） | その状態の 1 件だけ | 高 |
| I-111 | ListAsync | 日付（日本時間の暦日）の境界 | `2026-09-30T14:59:59.9999999Z`、`2026-09-30T15:00:00Z`、`2026-10-01T14:59:59.9999999Z`、`2026-10-01T15:00:00Z` の 4 件、`Date = 2026-10-01` | 2 件目と 3 件目だけ（JST の 10/1 0:00 以上 10/2 0:00 未満） | 高 |
| I-112 | ListAsync | UTC の暦日と日本時間の暦日のずれ | `2026-10-01T20:00:00Z`（JST では 10/2 5:00）、`Date = 2026-10-01` | 含まれない。`Date = 2026-10-02` なら含まれる | 高 |
| I-113 | ListAsync | From の境界 | `From` と同時刻の予約と 1 tick 前の予約 | 同時刻は含み、1 tick 前は含まない（以上） | 高 |
| I-114 | ListAsync | To の境界 | `To` と同時刻の予約と 1 tick 後の予約 | 同時刻は含み、1 tick 後は含まない（以下） | 高 |
| I-115 | ListAsync | From / To の Kind が Local | `DateTimeOffset(2026-10-01 09:00 +09:00).LocalDateTime` を `From` に指定 | `2026-10-01T00:00:00Z` 以降の予約が返る（パラメータにも UTC 変換が効く） | 中 |
| I-116 | ListAsync | From / To の Kind が Unspecified（現状の挙動） | `Kind = Unspecified` の `2026-10-01T00:00:00` を `From` に指定 | UTC の `2026-10-01T00:00:00Z` 以降として扱われる | 低 |
| I-117 | ListAsync | From が To より後 | `From = 10/2`、`To = 10/1` | 0 件、`Total` が 0（例外にならない） | 中 |
| I-118 | ListAsync | 条件の組み合わせ | `UserId`・`Date`・`Status`・`From`・`To` をすべて指定し、各条件だけ外れる予約を用意 | すべて満たす予約だけ（AND） | 中 |
| I-119 | ListAsync | 該当なし | 条件に合う予約がない | `Items` が空、`Total` が 0 | 中 |
| I-120 | ListAsync | 並び順 | 希望乗車日時がばらばらの予約を、日時と逆順に保存 | `RequestedPickupAt` の昇順 | 高 |
| I-121 | ListAsync | 同時刻の並び順 | 同じ `RequestedPickupAt` の予約 5 件 | `Id` の SQL Server 上の昇順（期待値は `SqlGuid` の比較で求める）。同じ条件で 2 回呼んでも同じ順 | 中 |
| I-122 | ListAsync | ページング | 5 件、`Limit = 2`、`Page = 1 / 2 / 3` | 2 件・2 件・1 件、`Total` は常に 5。3 ページ合わせて重複・欠落がなく I-120 の順 | 高 |
| I-123 | ListAsync | 範囲外のページ | 5 件、`Limit = 2`、`Page = 4` | `Items` が空、`Total` が 5 | 中 |
| I-124 | ListAsync | page の補正 | 5 件、`Limit = 2`、`Page = 0` / `-1`（Theory） | 1 ページ目と同じ 2 件 | 高 |
| I-125 | ListAsync | limit の補正 | 51 件、`Limit = 0` / `-1`（Theory） | 50 件、`Total` が 51 | 高 |
| I-126 | ListAsync | limit の上限なし（現状の挙動） | 60 件、`Limit = 1000` | 60 件すべて | 中 |
| I-127 | ListAsync | 既定値 | `new ReservationListFilter()` | `Page = 1`、`Limit = 50` として全ユーザーから返る | 中 |
| I-128 | ListAsync | 非常に大きい page | `Page = int.MaxValue`、`Limit = 50` | 現状の挙動（空か例外か）を確かめて固定する（[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |
| I-129 | ListAsync | 追跡しない | 一覧取得後の `db.ChangeTracker.Entries()` | 空（`AsNoTracking`） | 低 |

### 4.8 EfRideGroupRepository（EfRideGroupRepositoryTests）

運転手は `User.Create(..., UserRole.Driver)` で作って保存し、便は `RideGroup.Propose` → `AppDbContext.RideGroups.Add` → `ExecuteUpdateAsync` で状態を設定します。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-130 | HasUnfinishedByDriverAsync | 未完了 | `Confirmed` / `InProgress` の便が 1 件（Theory） | true | 高 |
| I-131 | HasUnfinishedByDriverAsync | 候補は含めない | `Proposed` の便だけ | false | 高 |
| I-132 | HasUnfinishedByDriverAsync | 完了・キャンセル | `Completed` / `Cancelled` の便だけ（Theory） | false | 高 |
| I-133 | HasUnfinishedByDriverAsync | 便なし | 便のない運転手 | false | 高 |
| I-134 | HasUnfinishedByDriverAsync | 他の運転手の便 | 別の運転手に `Confirmed` の便 | false | 高 |
| I-135 | HasUnfinishedByDriverAsync | 混在 | `Proposed` と `InProgress` が 1 件ずつ | true | 中 |
| I-136 | HasUnfinishedByDriverAsync | 状態の往復 | `Propose` の便を保存して別コンテキストで読む | `Status` が `Proposed`、`GroupNumber` / `DriverId` / 日時（`Kind` が `Utc`）が一致 | 中 |

### 4.9 DatabaseHealthCheck（DatabaseHealthCheckTests）

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| I-137 | CheckHealthAsync | 接続できる | コンテナの DB（作成済み） | `Healthy` | 高 |
| I-138 | CheckHealthAsync | DB がない | コンテナのサーバーに存在しない DB 名の接続文字列 | `Unhealthy`、`Description` が `データベースに接続できません` | 中 |
| I-139 | CheckHealthAsync | サーバーに届かない | `Server=127.0.0.1,1;Connect Timeout=2` などの到達できない接続文字列 | `Unhealthy`、`Description` が `データベースに接続できません`（例外にならない） | 高 |
| I-140 | CheckHealthAsync | 例外 | `Dispose` 済みの `AppDbContext`（`ObjectDisposedException` が起きる） | `Unhealthy`、`Description` が `データベースに接続できません`、`Exception` が null、`Description` に接続文字列やサーバー名を含まない | 高 |
| I-141 | CheckHealthAsync | 例外のログ | I-140 と同じ | `RecordingLogger` に `LogLevel.Error` のログが 1 件、例外オブジェクト付きで記録される | 中 |
| I-142 | CheckHealthAsync | 接続できないときのログ | I-138 と同じ | `CanConnectAsync` が false を返した経路ではログを出さない（現状の挙動） | 低 |
| I-143 | CheckHealthAsync | キャンセル（現状の挙動） | キャンセル済みの `CancellationToken` | `OperationCanceledException` が伝わらず `Unhealthy` が返る（[5章](#5-既知の問題仕様の曖昧な点) 参照） | 低 |

I-138 と I-139 は Docker を使わない経路もありますが、ネットワーク待ちがあるため `Database` カテゴリに入れます。

## 5. 既知の問題・仕様の曖昧な点

実装を読んで気づいた点です。挙動を断定できないものは「現状の挙動を確かめて固定する」テストにしています。

| # | 内容 | 関係するテスト |
| --- | --- | --- |
| 1 | 一意制約違反は `DbUpdateException` のまま返り、リポジトリで業務例外に変換していない。`RegisterUserUseCase` は事前に `FindByEmailAsync` で確かめるが、同じメールアドレスで同時に登録されると `EmailAlreadyRegisteredException` にならず 500 になる可能性がある。予約番号も `RR-{yyyyMMdd}-{16 進 4 桁}`（1 日 65,536 通り）で、衝突しても再試行しない | I-085, I-094 |
| 2 | 文字数の上限は DB の制約だけで検出される（`docs/ENDPOINT.md` の「既知の制約」と同じ）。また `nvarchar(n)` の長さは UTF-16 の単位なので、`𠮷` などサロゲートペアの文字は 1 文字で 2 と数えられる。画面側で文字数を数える方法と合わない可能性がある | I-068, I-070 |
| 3 | メールアドレスの大文字小文字・末尾の空白の扱いは DB の照合順序と SQL Server の比較規則に依存する。既定の `_CI_` 照合順序では、検索も一意制約も大文字小文字を区別しないので両者は一致しているが、DB の照合順序を変えると挙動が変わる。コードでは正規化（小文字化）していない | I-060, I-083, I-084, I-086 |
| 4 | `DateTimeKind.Unspecified` の値は変換されずにそのまま保存され、読み出すと UTC 扱いになる。`from` / `to` をタイムゾーンなしで受けた場合や、`requested_pickup_at` をタイムゾーンなしで登録した場合は UTC とみなされる（日本時間のつもりなら 9 時間ずれる）。仕様どおりかは要確認 | I-064, I-116 |
| 5 | `Local` の値の変換はテストを動かすホストのタイムゾーンに依存する。GitHub Actions などホストが UTC の環境では I-063 / I-115 が変換なしでも通ってしまうため、CI では環境変数 `TZ=Asia/Tokyo` を付けてテストプロセスを起動するのが望ましい | I-063, I-115 |
| 6 | 一覧の `date` は半開区間（JST の 0:00 以上 24:00 未満）、`to` は以下（含む）。`docs/ENDPOINT.md` の「以前」とは一致しているが、`date` と `to` で境界の考え方が違う | I-111, I-113, I-114 |
| 7 | `Skip((page - 1) * limit)` は `int` のまま掛け算するため、`page` が非常に大きいとオーバーフローして負の値になり、SQL Server の `OFFSET` がエラーになる可能性がある。`limit` にも上限がない（`docs/ENDPOINT.md` の「既知の制約」と同じ） | I-126, I-128 |
| 8 | 同時刻の並び順は `Id`（`uniqueidentifier`）で決まるが、SQL Server の `uniqueidentifier` の大小は .NET の `Guid.CompareTo` と異なる（末尾のバイトから比べる）。テストの期待値を .NET 側で並べ替えて作ると誤る。利用者から見た同時刻の順は実質ランダム | I-121 |
| 9 | `UpdateAsync` は切り離された状態のエンティティに `Update()` を使う。`Update()` はキーが設定済みのエンティティをすべて「更新」扱いにするため、`AddRole` で新しく作った `Rider` / `Driver` も INSERT されず UPDATE（0 行）になり、`DbUpdateConcurrencyException` になる可能性がある。今のユースケースは同じスコープで `FindByIdAsync` した追跡中のエンティティを渡すので表に出ない | I-088, I-091 |
| 10 | `SaveChangesAsync` が失敗しても、失敗したエンティティは追跡から外れない。同じ `DbContext` を使い続けると次の保存でも同じエラーになる。今はリクエストごとにスコープが終わるので影響は小さい | I-087 |
| 11 | 区分の値変換は `"driver"` 以外をすべて `Rider` として読む（大文字の `"Driver"` も）。状態の値変換は不明な値で例外になるので扱いが非対称。DB に `status` / `active_role` の CHECK 制約はない | I-052, I-072, I-073 |
| 12 | `RideGroup` には状態を変えるメソッドがなく、`IRideGroupRepository` にも追加・更新のメソッドがない。テストデータは `DbContext` を直接使って作るしかない | I-130〜I-136 |
| 13 | `JwtOptions.Validate` は SigningKey のバイト数だけを見るので、空白だけの鍵も通る。SigningKey に null を代入した場合は `InvalidOperationException` ではなく `ArgumentNullException` になる（設定ファイルからのバインドでは通常空文字になる）。`JwtAccessTokenIssuer` 自身は `Validate` を呼ばない | I-013, I-029 |
| 14 | `JwtAccessTokenIssuer` は `DateTime.UtcNow` を直接使うので時刻を固定できない。また `exp` は秒単位に切り捨てられるため、レスポンスの `expires_at` はトークンの `exp` より最大 1 秒遅い | I-018, I-020 |
| 15 | `AspNetPasswordHasher.Verify` は `SuccessRehashNeeded` も成功とし、再ハッシュして保存し直す仕組みはない。不正な形式のハッシュを渡したときに例外になるか false になるかは `PasswordHasher` の実装次第で、例外ならログインが 500 になる | I-040, I-041 |
| 16 | `DatabaseHealthCheck` は `catch (Exception)` でキャンセル（`OperationCanceledException`）も `Unhealthy` に変える。また本番は `EnableRetryOnFailure` 付きなので、DB に届かないときに `/health` の応答まで時間がかかる可能性がある（要確認） | I-143 |
| 17 | `docs/DATA_MODEL.md` の ER 図にある `users.updated_at` や、`ride_groups` の `vehicle_id` / `planned_departure_at` などはマッピングされていない。テストは今のコード（`AppDbContext`）を正とする | I-042〜I-050 |
| 18 | スキーマは `EnsureCreated` で作っている（マイグレーション未導入）。マイグレーションを入れたら、テストの DB 作成も `MigrateAsync` に切り替える | I-059 |

## 6. 対象外

| 対象 | 理由 |
| --- | --- |
| 例外から HTTP ステータスへの変換、`meta.page` / `meta.limit` の値 | handler 層で扱う |
| 業務例外の送出、ユースケースの呼び出し順 | usecase 層で扱う |
| ドメインの検証・状態遷移ルール | domain 層で扱う |
| `Program.cs` の DI 登録、JwtBearer の設定（`MapInboundClaims = false` など）、起動時の `EnsureCreated` | 結合テストの範囲 |
| `EnableRetryOnFailure` の再試行 | SQL Server の一時的な障害を安定して再現できない |
| 同時実行（同時登録による一意制約の競合など）、性能、大量データ | 単体テストで安定して再現できない。問題は [5章](#5-既知の問題仕様の曖昧な点) に記載 |
| ユーザー・予約の削除、Cascade 削除の動き | リポジトリに削除の操作がない。削除動作の設定値は I-049 で確認する |
| PBKDF2 の反復回数などハッシュアルゴリズム自体、JWT の署名計算 | ASP.NET Core Identity / `Microsoft.IdentityModel` の責務 |
| Azure SQL Database 固有の挙動 | テストは SQL Server 2022 のコンテナで行う。照合順序などの差は I-060 の前提確認で検出する |
| `docs/DATA_MODEL.md` にあって未実装のテーブル（`group_members`、`vehicles` など） | コードが存在しない |
