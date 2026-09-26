# usecase 層 単体テスト設計書

`backend/usecase/` のユースケースに対する単体テストの設計です。
domain / handler / infrastructure 層のテスト設計書と同じルールで書いています。

> usecase のコードを変更したら、このファイルのテストケースも見直してください。

## 目次

1. [目的と範囲](#1-目的と範囲)
2. [テスト方針](#2-テスト方針)
3. [テスト対象一覧](#3-テスト対象一覧)
4. [テストケース](#4-テストケース)
5. [既知の問題・仕様の曖昧な点](#5-既知の問題仕様の曖昧な点)
6. [対象外](#6-対象外)

## 1. 目的と範囲

### 目的

- 各ユースケースが、ドメインとリポジトリを正しい順序・条件で呼び出していることを確かめる
- 例外の種類（handler が HTTP ステータスに変換する元）が仕様どおりであることを確かめる
- 失敗したときにリポジトリへの保存（`AddAsync` / `UpdateAsync`）が呼ばれないことを確かめる

### 範囲

| ファイル | 内容 |
| --- | --- |
| `Auth.cs` | ログイン（`LoginUseCase`） |
| `User.cs` | 利用者登録、ログイン中ユーザーの取得、区分の追加、稼働区分の切り替え |
| `Reservation.cs` | 予約の登録・一覧・詳細・キャンセル |
| `RideGroup.cs` | `IRideGroupRepository` の定義のみ（ユースケースなし。Fake を用意するだけ） |

## 2. テスト方針

### ツール

| 項目 | 内容 |
| --- | --- |
| テストプロジェクト | `tests/backend.Tests/`（`backend/` の中に置くと `backend.csproj` が `**/*.cs` を取り込むため外に置く） |
| フォルダ | 層ごとに `Domain/`, `Usecase/`, `Handler/`, `Infrastructure/`。この層は `tests/backend.Tests/Usecase/` |
| フレームワーク | xUnit |
| アサーション | xUnit 標準の `Assert`（FluentAssertions は v8 から商用ライセンスのため使わない） |
| モックライブラリ | 使わない |
| テストクラス名 | `{対象クラス}Tests`（例：`CancelReservationUseCaseTests`） |
| テストメソッド名 | `{メソッド}_{条件}_{期待結果}`（日本語可。例：`ExecuteAsync_確定済みの予約_キャンセルになり保存される`） |
| テストケース ID | `U-001` から連番 |
| 優先度 | 高 / 中 / 低 |

### テストダブルの方針

usecase が依存するインターフェースは、`tests/backend.Tests/TestDoubles/` に置く手書きのインメモリ実装（Fake）で差し替えます。
DB・PBKDF2・JWT の実物は使いません。

| Fake | 実装するインターフェース | 振る舞い | 呼び出し記録 |
| --- | --- | --- | --- |
| `FakeUserRepository` | `Usecase.User.IUserRepository` | `Dictionary<Guid, User>` に保持。`Seed(user)` で事前登録。`FindByEmailAsync` は `Email` と引数の完全一致（`StringComparison.Ordinal`）で探す。`AddAsync` / `UpdateAsync` は辞書に入れる | `FindByIdCalls`（引数の `Guid` 一覧）、`FindByEmailCalls`（引数の文字列一覧）、`Added`、`Updated`（渡された `User` の一覧） |
| `FakeReservationRepository` | `Usecase.Reservation.IReservationRepository` | `Dictionary<Guid, Reservation>` に保持。`Seed(reservation)` で事前登録。`ListAsync` は `ListResult` に設定した値をそのまま返す。`HasUnfinishedAsync` は `UsersWithUnfinished`（`HashSet<Guid>`）に含まれるかを返す | `ListCalls`（渡された `ReservationListFilter`）、`HasUnfinishedCalls`、`Added`、`Updated` |
| `FakeRideGroupRepository` | `Usecase.RideGroup.IRideGroupRepository` | `DriversWithUnfinished`（`HashSet<Guid>`）に含まれるかを返す | `HasUnfinishedByDriverCalls` |
| `FakePasswordHasher` | `Usecase.User.IPasswordHasher` | `Hash(p)` は `"hashed:" + p` を返す。`Verify(h, p)` は `h == "hashed:" + p` を返す | `HashCalls`、`VerifyCalls`（`(passwordHash, password)` の組） |
| `FakeAccessTokenIssuer` | `Usecase.Auth.IAccessTokenIssuer` | `new AccessToken($"token-{user.Id}", 固定日時)` を返す | `IssuedFor`（渡された `User` の一覧） |

補助として、テストデータを作るビルダーも `TestDoubles/` に置きます。

| ビルダー | 内容 |
| --- | --- |
| `TestUsers` | `Rider()`（Rider のみ）、`Driver()`（Driver のみ）、`Both(UserRole active)`（両方を持ち、指定した区分で稼働）。`User.Create` → `AddRole` → `SwitchRole` で作る。パスワードハッシュは `FakePasswordHasher` と同じ形式（`"hashed:password1234"`） |
| `TestReservations` | `InStatus(ReservationStatus status, Guid userId)`。`Reservation.Create` のあと `Confirm` / `StartInProgress` / `Complete` / `Cancel` を順に呼んで目的の状態にする |

Fake は受け取ったオブジェクトの参照をそのまま保持します。ドメインのメソッドは同じインスタンスを書き換えるため、「保存されていない」ことは状態ではなく `Added` / `Updated` の件数で判定します。

### 名前の衝突

`Usecase.User`（名前空間）と `Domain.Users.User`（クラス）が衝突するため、テストコードでも本体と同じく `using DomainUser = Domain.Users.User;` のようなエイリアスを使います。

### 書き方の例

```csharp
public class CancelReservationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_乗車中の予約_InvalidOperationExceptionで保存されない()
    {
        var userId = Guid.NewGuid();
        var reservation = TestReservations.InStatus(ReservationStatus.InProgress, userId);
        var repository = new FakeReservationRepository();
        repository.Seed(reservation);
        var useCase = new CancelReservationUseCase(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(reservation.Id, userId, "予定変更"));

        Assert.Empty(repository.Updated);
        Assert.Equal(ReservationStatus.InProgress, reservation.Status);
    }
}
```

### この層で書かないこと

- ドメインの入力検証の網羅（空文字・カタカナ判定・状態遷移の全組み合わせ）。domain 層のテストで扱い、ここでは「ドメインの例外がそのまま伝わり、保存されない」ことを代表値で確かめるだけにする
- HTTP ステータス・レスポンス形式への変換（handler 層）
- 実際のハッシュ化・JWT の署名、SQL の絞り込み・ページング・一意制約（infrastructure 層）

## 3. テスト対象一覧

| ファイル | クラス | メソッド | 依存 | テストクラス |
| --- | --- | --- | --- | --- |
| `Auth.cs` | `LoginUseCase` | `ExecuteAsync(email, password)` | `IUserRepository`, `IPasswordHasher`, `IAccessTokenIssuer` | `LoginUseCaseTests` |
| `User.cs` | `RegisterUserUseCase` | `ExecuteAsync(email, password, lastName, firstName, kanaLastName, kanaFirstName, role)` | `IUserRepository`, `IPasswordHasher` | `RegisterUserUseCaseTests` |
| `User.cs` | `GetCurrentUserUseCase` | `ExecuteAsync(subject)` | `IUserRepository` | `GetCurrentUserUseCaseTests` |
| `User.cs` | `AddUserRoleUseCase` | `ExecuteAsync(id, role)` | `IUserRepository` | `AddUserRoleUseCaseTests` |
| `User.cs` | `SwitchUserRoleUseCase` | `ExecuteAsync(id, role)` | `IUserRepository`, `IReservationRepository`, `IRideGroupRepository` | `SwitchUserRoleUseCaseTests` |
| `Reservation.cs` | `RegisterReservationUseCase` | `ExecuteAsync(userId, pickupLocation, destination, requestedPickupAt, passengerCount, considerationNotes)` | `IReservationRepository`, `IUserRepository` | `RegisterReservationUseCaseTests` |
| `Reservation.cs` | `ListReservationsUseCase` | `ExecuteAsync(filter)` | `IReservationRepository` | `ListReservationsUseCaseTests` |
| `Reservation.cs` | `GetReservationUseCase` | `ExecuteAsync(id, userId)` | `IReservationRepository` | `GetReservationUseCaseTests` |
| `Reservation.cs` | `CancelReservationUseCase` | `ExecuteAsync(id, userId, reason)` | `IReservationRepository` | `CancelReservationUseCaseTests` |

usecase で定義している例外

| 例外 | 基底クラス | 投げる場所 |
| --- | --- | --- |
| `InvalidCredentialsException` | `Exception` | `LoginUseCase` |
| `UserNotFoundException`（`UserId`） | `Exception` | `RegisterReservationUseCase`, `AddUserRoleUseCase`, `SwitchUserRoleUseCase` |
| `EmailAlreadyRegisteredException`（`Email`） | `Exception` | `RegisterUserUseCase` |
| `UnfinishedActivityExistsException`（`UserId`, `ActiveRole`） | `InvalidOperationException` | `SwitchUserRoleUseCase` |
| `ReservationNotFoundException`（`ReservationId`） | `Exception` | `GetReservationUseCase`, `CancelReservationUseCase` |

## 4. テストケース

特に書いていない限り、ユーザーは `TestUsers`、予約は `TestReservations` で作り、Fake に `Seed` しておきます。
「保存されない」は `Added` / `Updated` が空であることを指します。

### 4.1 LoginUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-001 | `ExecuteAsync` | 正常系 | 登録済みユーザー（`"hashed:password1234"`）。正しいメールアドレスとパスワード | `FakeAccessTokenIssuer` が返した `AccessToken` がそのまま返る。`IssuedFor` にそのユーザーが1件 | 高 |
| U-002 | `ExecuteAsync` | 入力の整形 | メールアドレスの前後に空白（`"  taro@example.com "`） | 成功する。`FindByEmailCalls` の引数は前後の空白を除いた値 | 中 |
| U-003 | `ExecuteAsync` | 異常系（空のメールアドレス） | `email` が `null` / `""` / `"   "`（Theory） | `InvalidCredentialsException`。`FindByEmailCalls` が空、`IssuedFor` が空 | 高 |
| U-004 | `ExecuteAsync` | 異常系（空のパスワード） | `password` が `null` / `""`（Theory） | `InvalidCredentialsException`。`FindByEmailCalls` が空、`IssuedFor` が空 | 高 |
| U-005 | `ExecuteAsync` | 境界（空白のみのパスワード） | `password` が `"   "`、ユーザーは登録済み | 事前チェックでは弾かれず `Verify` が呼ばれる。Fake では一致しないので `InvalidCredentialsException` | 低 |
| U-006 | `ExecuteAsync` | 異常系（未登録） | 登録されていないメールアドレス | `InvalidCredentialsException`。`VerifyCalls` が空、`IssuedFor` が空 | 高 |
| U-007 | `ExecuteAsync` | 異常系（パスワード違い） | 登録済みのメールアドレス、違うパスワード | `InvalidCredentialsException`。`IssuedFor` が空 | 高 |
| U-008 | `ExecuteAsync` | 依存の呼び方 | 正しい資格情報 | `VerifyCalls` が `(user.PasswordHash, 入力したパスワード)` の順で1件 | 中 |
| U-009 | `ExecuteAsync` | 入力の整形（パスワードはそのまま） | `password` が `" password1234 "` | `Verify` には前後の空白を含んだまま渡る（トリムしない） | 低 |
| U-010 | `ExecuteAsync` | 原因を区別しない | U-006 と U-007 の例外 | 例外の型とメッセージ（`Email or password is incorrect.`）が同じ | 中 |
| U-011 | `ExecuteAsync` | 稼働区分に関係なくログインできる | `TestUsers.Driver()`（Driver で稼働） | 成功し、トークンが返る | 低 |

### 4.2 RegisterUserUseCase

入力の既定値は `email = "taro@example.com"`, `password = "password1234"`, `lastName = "山田"`, `firstName = "太郎"`, `kanaLastName = "ヤマダ"`, `kanaFirstName = "タロウ"`, `role = Rider` とし、ケースごとに1項目だけ変えます。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-012 | `ExecuteAsync` | 正常系（Rider） | 既定値 | 返り値の `ActiveRole` が `Rider`、`Roles` が `[Rider]`、`Rider` プロフィールあり。`Added` にそのユーザーが1件 | 高 |
| U-013 | `ExecuteAsync` | 正常系（Driver） | `role = Driver` | `ActiveRole` が `Driver`、`Roles` が `[Driver]`。`Added` が1件 | 高 |
| U-014 | `ExecuteAsync` | 平文を保存しない | 既定値 | `PasswordHash` が `"hashed:password1234"`（平文と異なる）。`HashCalls` が平文のパスワードで1件 | 高 |
| U-015 | `ExecuteAsync` | 境界値（下限） | パスワード 8 文字 | 成功し、`Added` が1件 | 高 |
| U-016 | `ExecuteAsync` | 境界値（上限） | パスワード 128 文字 | 成功し、`Added` が1件 | 高 |
| U-017 | `ExecuteAsync` | 境界値（下限-1） | パスワード 7 文字 | `ArgumentException`（`ParamName` が `"password"`）。`FindByEmailCalls`・`HashCalls`・`Added` が空 | 高 |
| U-018 | `ExecuteAsync` | 境界値（上限+1） | パスワード 129 文字 | U-017 と同じ | 高 |
| U-019 | `ExecuteAsync` | 異常系（パスワードなし） | `password` が `null` / `""`（Theory） | U-017 と同じ | 中 |
| U-020 | `ExecuteAsync` | 異常系（メールアドレス重複） | 同じメールアドレスのユーザーを Seed | `EmailAlreadyRegisteredException`（`Email` が入力値）。`HashCalls`・`Added` が空 | 高 |
| U-021 | `ExecuteAsync` | 重複判定の前にトリム | Seed は `"taro@example.com"`、入力は `" taro@example.com "` | `EmailAlreadyRegisteredException`（`Email` はトリム後の値）。`FindByEmailCalls` の引数はトリム後 | 中 |
| U-022 | `ExecuteAsync` | チェックの順序 | パスワード 7 文字かつメールアドレス重複 | `ArgumentException`（パスワードのエラーが先）。`FindByEmailCalls` が空 | 中 |
| U-023 | `ExecuteAsync` | 異常系（空のメールアドレス） | `email` が `null` / `""` / `"   "`（Theory） | 重複チェックを飛ばす（`FindByEmailCalls` が空）。ドメインで `ArgumentException`（`ParamName` が `"email"`）。`Added` が空 | 高 |
| U-024 | `ExecuteAsync` | 異常系（姓名が空） | `lastName` または `firstName` が `""` / `"   "`（Theory） | `ArgumentException`（`ParamName` が該当項目）。`Added` が空 | 中 |
| U-025 | `ExecuteAsync` | 異常系（読み仮名） | `kanaLastName` または `kanaFirstName` がひらがな・半角カナ・空（Theory） | `ArgumentException`（`ParamName` が該当項目）。`Added` が空 | 中 |
| U-026 | `ExecuteAsync` | 保存値の整形 | `email` と姓名の前後に空白 | 返り値の `Email`・`LastName`・`FirstName` は前後の空白が除かれている | 低 |
| U-027 | `ExecuteAsync` | 境界（空白のみのパスワード） | パスワードが空白 8 文字 | 長さチェックを通過して登録される（現状の挙動の確認） | 低 |
| U-028 | `ExecuteAsync` | 境界（サロゲートペア） | 絵文字 4 文字（`string.Length` は 8） | 登録される（`Length` は UTF-16 の単位で数える。現状の挙動の確認） | 低 |
| U-029 | `ExecuteAsync` | 異常系（定義外の区分） | `role = (UserRole)99` | ドメインの `AddRole` で `ArgumentOutOfRangeException`。`Added` が空 | 低 |

### 4.3 GetCurrentUserUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-030 | `ExecuteAsync` | 正常系 | 登録済みユーザーの `Id` の文字列 | そのユーザーが返る。`FindByIdCalls` にその `Id` が1件 | 高 |
| U-031 | `ExecuteAsync` | 存在しないユーザー | UUID 形式だが未登録の `Id` | `null`（例外にならない） | 高 |
| U-032 | `ExecuteAsync` | 不正な subject | `null` / `""` / `"abc"`（Theory） | `null`。`FindByIdCalls` が空 | 高 |
| U-033 | `ExecuteAsync` | UUID の表記ゆれ | ハイフンなし（`"N"` 形式）・波かっこ付き（`"B"` 形式）の `Id` | `Guid.TryParse` が受け付けるため、そのユーザーが返る | 低 |

### 4.4 AddUserRoleUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-034 | `ExecuteAsync` | 正常系（Driver を追加） | `TestUsers.Rider()`、`role = Driver` | `Roles` が `[Rider, Driver]`、`ActiveRole` は `Rider` のまま。`Updated` に1件 | 高 |
| U-035 | `ExecuteAsync` | 正常系（Rider を追加） | `TestUsers.Driver()`、`role = Rider` | `Roles` が `[Rider, Driver]`、`ActiveRole` は `Driver` のまま。`Updated` に1件 | 中 |
| U-036 | `ExecuteAsync` | 異常系（ユーザーなし） | 未登録の `Id` | `UserNotFoundException`（`UserId` が入力値）。`Updated` が空 | 高 |
| U-037 | `ExecuteAsync` | 異常系（登録済みの区分） | `TestUsers.Rider()`、`role = Rider` | `InvalidOperationException`。`Updated` が空 | 高 |
| U-038 | `ExecuteAsync` | 異常系（定義外の区分） | `role = (UserRole)99` | `ArgumentOutOfRangeException`。`Updated` が空 | 低 |

### 4.5 SwitchUserRoleUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-039 | `ExecuteAsync` | 正常系（Rider → Driver） | `TestUsers.Both(Rider)`、未完了の予約なし、`role = Driver` | `ActiveRole` が `Driver`。`Updated` に1件。`HasUnfinishedCalls` にユーザーの `Id` が1件、`HasUnfinishedByDriverCalls` は空 | 高 |
| U-040 | `ExecuteAsync` | 正常系（Driver → Rider） | `TestUsers.Both(Driver)`、未完了の運行なし、`role = Rider` | `ActiveRole` が `Rider`。`Updated` に1件。`HasUnfinishedByDriverCalls` にユーザーの `Id` が1件、`HasUnfinishedCalls` は空 | 高 |
| U-041 | `ExecuteAsync` | 異常系（未完了の予約） | `TestUsers.Both(Rider)`、`UsersWithUnfinished` にユーザーを追加、`role = Driver` | `UnfinishedActivityExistsException`（`UserId` が入力値、`ActiveRole` が `Rider`）。`ActiveRole` は変わらず、`Updated` が空 | 高 |
| U-042 | `ExecuteAsync` | 異常系（未完了の運行） | `TestUsers.Both(Driver)`、`DriversWithUnfinished` にユーザーを追加、`role = Rider` | `UnfinishedActivityExistsException`（`ActiveRole` が `Driver`）。`Updated` が空 | 高 |
| U-043 | `ExecuteAsync` | 同じ区分を指定 | `TestUsers.Both(Rider)`、`UsersWithUnfinished` にユーザーを追加、`role = Rider` | 成功し、`ActiveRole` は `Rider` のまま。未完了チェックは呼ばれない（両方の呼び出し記録が空）。`Updated` は1件（→ 5 章） | 中 |
| U-044 | `ExecuteAsync` | 異常系（切り替え先が未登録） | `TestUsers.Rider()`、未完了なし、`role = Driver` | `InvalidOperationException`（`UnfinishedActivityExistsException` ではないこと）。`Updated` が空 | 高 |
| U-045 | `ExecuteAsync` | チェックの順序 | `TestUsers.Rider()`、`UsersWithUnfinished` にユーザーを追加、`role = Driver` | 未登録より未完了が先に判定され、`UnfinishedActivityExistsException`。`Updated` が空 | 中 |
| U-046 | `ExecuteAsync` | 異常系（ユーザーなし） | 未登録の `Id` | `UserNotFoundException`。`HasUnfinishedCalls`・`HasUnfinishedByDriverCalls`・`Updated` が空 | 高 |

### 4.6 RegisterReservationUseCase

入力の既定値は `pickupLocation = "市役所前"`, `destination = "中央病院"`, `requestedPickupAt = 2026-10-01T00:00:00Z`, `passengerCount = 2`, `considerationNotes = "車椅子を使用"` とし、ユーザーは `TestUsers.Rider()` とします。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-047 | `ExecuteAsync` | 正常系 | 既定値 | 返り値の `Status` が `Matching`、`UserId` が入力値、各項目が入力どおり、`CancellationReason`・`CancelledAt` が `null`。`Added` に返り値と同じインスタンスが1件 | 高 |
| U-048 | `ExecuteAsync` | 予約番号の形式 | 既定値。実行前後の `DateTime.UtcNow` を記録 | `ReservationNumber` が `^RR-\d{8}-[0-9A-F]{4}$` に一致し、日付部分が実行前後どちらかの UTC 日付（`yyyyMMdd`）と一致 | 中 |
| U-049 | `ExecuteAsync` | 配慮事項なし | `considerationNotes = null` | 成功し、`ConsiderationNotes` が `null`。`Added` が1件 | 中 |
| U-050 | `ExecuteAsync` | 境界値（乗車人数の下限） | `passengerCount = 1` | 成功し、`Added` が1件 | 高 |
| U-051 | `ExecuteAsync` | 境界値（下限-1） | `passengerCount` が `0` / `-1`（Theory） | `ArgumentException`（`ParamName` が `"passengerCount"`）。`Added` が空 | 高 |
| U-052 | `ExecuteAsync` | 異常系（乗車地が空） | `pickupLocation` が `""` / `"   "`（Theory） | `ArgumentException`（`ParamName` が `"pickupLocation"`）。`Added` が空 | 中 |
| U-053 | `ExecuteAsync` | 異常系（目的地が空） | `destination` が `""` / `"   "`（Theory） | `ArgumentException`（`ParamName` が `"destination"`）。`Added` が空 | 中 |
| U-054 | `ExecuteAsync` | 異常系（ユーザーなし） | 未登録の `userId` | `UserNotFoundException`（`UserId` が入力値）。`Added` が空 | 高 |
| U-055 | `ExecuteAsync` | 異常系（Driver で稼働中） | `TestUsers.Both(Driver)`（Rider プロフィールはある） | `InvalidOperationException`。`Added` が空 | 高 |
| U-056 | `ExecuteAsync` | 異常系（Driver のみ） | `TestUsers.Driver()` | `InvalidOperationException`。`Added` が空 | 中 |
| U-057 | `ExecuteAsync` | チェックの順序 | `TestUsers.Driver()`、`passengerCount = 0` | 入力検証より稼働区分のチェックが先で、`InvalidOperationException` | 中 |
| U-058 | `ExecuteAsync` | 過去の日時 | `requestedPickupAt` が現在より前 | 登録される（usecase・domain とも日時を検証しない。現状の挙動の確認） | 低 |
| U-059 | `ExecuteAsync` | 入力をトリムしない | `pickupLocation = " 市役所前 "` | `PickupLocation` は前後の空白を含んだまま（User と扱いが異なる。現状の挙動の確認） | 低 |

### 4.7 ListReservationsUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-060 | `ExecuteAsync` | 条件をそのまま渡す | すべての項目を埋めた `ReservationListFilter`。`ListResult` に予約2件・`Total = 5` を設定 | `ListCalls` に同じ値の `filter` が1件（record の等価比較）。返り値の `Items`・`Total` は `ListResult` と同じ | 高 |
| U-061 | `ExecuteAsync` | 0 件 | `ListResult` が空・`Total = 0` | 空の `Items` と `Total = 0` が返る | 中 |
| U-062 | `ExecuteAsync` | 補正しない | `Page = 0`, `Limit = 0` | `ListCalls` の `Page`・`Limit` は `0` のまま（補正はリポジトリの責務） | 低 |

### 4.8 GetReservationUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-063 | `ExecuteAsync` | 正常系 | 自分の予約の `Id` と自分の `userId` | その予約が返る | 高 |
| U-064 | `ExecuteAsync` | 異常系（存在しない） | 未登録の予約 `Id` | `ReservationNotFoundException`（`ReservationId` が入力値） | 高 |
| U-065 | `ExecuteAsync` | 異常系（他人の予約） | 他人の予約の `Id` と自分の `userId` | `ReservationNotFoundException`。例外の型とメッセージの形が U-064 と同じ（存在を知らせない） | 高 |
| U-066 | `ExecuteAsync` | 状態に関係なく取得できる | `Cancelled` / `Completed` の自分の予約（Theory） | その予約が返る | 低 |

### 4.9 CancelReservationUseCase

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| U-067 | `ExecuteAsync` | 正常系（マッチング中） | `Matching` の自分の予約、`reason = "予定変更"` | `Status` が `Cancelled`、`CancellationReason` が `"予定変更"`、`CancelledAt` が実行前後の時刻の間。`Updated` に1件 | 高 |
| U-068 | `ExecuteAsync` | 正常系（確定） | `Confirmed` の自分の予約 | `Status` が `Cancelled`。`Updated` に1件 | 高 |
| U-069 | `ExecuteAsync` | 理由なし | `reason = null` | 成功し、`CancellationReason` が `null`。`Updated` に1件 | 中 |
| U-070 | `ExecuteAsync` | 異常系（乗車中） | `InProgress` の自分の予約 | `InvalidOperationException`。`Status` は変わらず、`Updated` が空 | 高 |
| U-071 | `ExecuteAsync` | 異常系（完了） | `Completed` の自分の予約 | `InvalidOperationException`。`Updated` が空 | 高 |
| U-072 | `ExecuteAsync` | 異常系（二重キャンセル） | `Cancelled` の自分の予約 | `InvalidOperationException`。`CancellationReason`・`CancelledAt` は最初のキャンセル時の値のまま、`Updated` が空 | 高 |
| U-073 | `ExecuteAsync` | 異常系（存在しない） | 未登録の予約 `Id` | `ReservationNotFoundException`。`Updated` が空 | 高 |
| U-074 | `ExecuteAsync` | 異常系（他人の予約） | 他人の `Matching` の予約 | `ReservationNotFoundException`。その予約の `Status` は `Matching` のまま、`Updated` が空 | 高 |

合計 74 ケース（高 42 / 中 19 / 低 13）。

## 5. 既知の問題・仕様の曖昧な点

実装を読んで気づいた点です。テストでは現状の挙動を確かめ、仕様として正しいかは別途確認します。

| # | 内容 | テスト上の注意 |
| --- | --- | --- |
| 1 | `UnfinishedActivityExistsException` は `InvalidOperationException` を継承している。handler は派生クラスを先に catch している | `Assert.ThrowsAsync<T>` は型の完全一致で判定するので、U-044 のように基底の `InvalidOperationException` を期待するケースで派生型が混ざらないことを確かめられる。`ThrowsAnyAsync` は使わない |
| 2 | 予約番号の末尾は `Guid` から取った 16 進 4 桁で、同じ日に 65,536 通りしかない。usecase は重複を確認・再試行しない | 一意性は DB の UNIQUE 制約頼みで、単体テストでは検証できない。U-048 は形式だけを確かめ、2 件の番号が異なることは確かめない |
| 3 | 予約番号の日付と各種日時は `DateTime.UtcNow` を直接使っており、時刻を差し替えられない | 実行前後の時刻で範囲を確かめる。UTC の日付が変わる瞬間でも落ちないよう、U-048 は前後どちらかの日付と一致すればよいとする |
| 4 | メールアドレスはトリムするだけで、大文字小文字をそろえない。重複判定・ログインの一致はリポジトリの比較方法に依存する（`EfUserRepository` は `==`） | Fake は完全一致で実装する。大文字小文字違いを同じとみなすかは仕様が決まっていないため、usecase のテストケースには入れない |
| 5 | 利用者登録の重複チェックと `AddAsync` はアトミックではない。同時に同じメールアドレスで登録すると、DB の一意制約違反になる | usecase は例外を変換しないので、単体テストでは扱わない |
| 6 | パスワードの長さは `string.Length`（UTF-16 の単位）で数える。空白だけのパスワードも通る。一方ログインは空白だけのパスワードを事前には弾かない | U-027・U-028・U-005 で現状の挙動を記録する。仕様として許すかは未確認 |
| 7 | 利用者登録では `_passwordHasher.Hash` が `User.Create` の引数として先に評価されるため、姓名や読み仮名が不正でもハッシュ化は実行される | U-024・U-025 では `HashCalls` が空であることを期待しない |
| 8 | 稼働区分の切り替えで今と同じ区分を指定すると、状態は変わらないが `UpdateAsync` は呼ばれる。ENDPOINT.md の「何も変えずに成功」とは、保存の有無の点で解釈が分かれる | U-043 は現状どおり `Updated` が1件であることを確かめる。保存しない実装に変える場合はこのケースも直す |
| 9 | 稼働区分の切り替えは、切り替え先の区分が未登録かどうかより先に未完了の予約・運行を確認する。未登録かつ未完了ありのときは「未完了」のエラーになる | U-045 で順序を固定する |
| 10 | `ListReservationsUseCase` は `filter.UserId` を強制しない。`UserId` が `null` だと全ユーザーの予約が対象になり得る（handler はログイン中のユーザーの `Id` を入れている） | usecase のテストは受け渡しだけを確かめる。本人の予約に限ることは handler 層で確かめる |
| 11 | 予約登録で希望乗車日時の過去・未来や `DateTimeKind` を検証しない。乗車人数の上限（REQUIREMENTS.md FR-R04「車両定員以内」）もまだない | U-058 は現状の挙動の記録。上限を実装したら境界値のケースを追加する |
| 12 | 予約の文字列（乗車地など）はトリムしないが、User の姓名はトリムする | U-059 で現状の挙動を記録する |
| 13 | 定義外の `UserRole`（例：`(UserRole)99`）はドメインの `AddRole` で `ArgumentOutOfRangeException` になる。これは `ArgumentException` の派生なので、handler で 422 扱いになり得る。通常は handler の列挙値チェックで先に弾かれる | U-029・U-038 は優先度 低 |
| 14 | Fake は参照を保持し、ドメインのメソッドは同じインスタンスを書き換える | 「保存されない」は `Added` / `Updated` の件数で判定し、Fake 内の状態では判定しない |

## 6. 対象外

| 対象 | 理由 |
| --- | --- |
| `SwitchUserRoleUseCase.HasUnfinishedAsync` の `ArgumentOutOfRangeException` の分岐 | `ActiveRole` は `User.Create` / `SwitchRole` で登録済みの区分にしかならず、定義外の値の `User` をテストから作れないため到達できない |
| `GetReservationUseCase.FindOwnedAsync`（`internal static`）の単体での呼び出し | `GetReservationUseCase` と `CancelReservationUseCase` を通して確かめる（U-063〜U-065、U-073・U-074） |
| `RideGroup.cs` のユースケース | インターフェースしかなく、ユースケースのクラスがない。`FakeRideGroupRepository` は `SwitchUserRoleUseCase` のテストで使う |
| ドメインの入力検証・状態遷移の網羅 | domain 層のテストで扱う。ここでは代表値で「例外が伝わり保存されない」ことだけを確かめる |
| パスワードのハッシュ化（PBKDF2）と JWT の発行・署名 | infrastructure 層（`backend/infrastructure/Auth.cs`）のテストで扱う |
| 一覧の絞り込み（日本時間の日付、状態、期間）、ページングの補正、並び順 | `EfReservationRepository` の責務のため infrastructure 層で扱う |
| 「未完了」の定義（完了・キャンセル以外の予約、確定済み・運行中の便） | リポジトリのクエリの責務のため infrastructure 層で扱う |
| メールアドレス・予約番号の一意制約、同時実行 | DB の制約に依存し、インメモリの Fake では再現できない |
| 例外から HTTP ステータス・エラーコードへの変換 | handler 層で扱う |
