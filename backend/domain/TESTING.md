# domain 層 単体テスト設計書

`backend/domain/` のエンティティ（`User` / `Rider` / `Driver` / `Reservation` / `RideGroup`）に対する単体テストの設計です。

## 1. 目的と範囲

- ドメインモデルが持つ **生成時のバリデーション**、**状態遷移のルール**、**利用者区分（ロール）のルール** が、実装どおり・仕様どおりに動くことを確認する。
- 対象は `backend/domain/` 以下の全ファイル。

| ファイル | 名前空間 | 主な責務 |
| --- | --- | --- |
| `Users.cs` | `Domain.Users` | `User` 集約。入力検証、ロールの追加・切り替え・稼働確認 |
| `Rider.cs` | `Domain.Users` | 利用者プロフィール（`User` 経由でのみ生成） |
| `Driver.cs` | `Domain.Users` | 運転手プロフィール（`User` 経由でのみ生成） |
| `Reservation.cs` | `Domain.Reservations` | 予約。入力検証と状態遷移（確定・運行開始・完了・キャンセル） |
| `RideGroup.cs` | `Domain.RideGroups` | 乗合の便。候補（`Proposed`）の生成のみ |

参考: [ENDPOINT.md](../../docs/ENDPOINT.md)、[DATA_MODEL.md](../../docs/DATA_MODEL.md)、[REQUIREMENTS.md](../../docs/REQUIREMENTS.md)（7. 状態定義）

## 2. テスト方針

### ツールと配置

| 項目 | 内容 |
| --- | --- |
| テストプロジェクト | `tests/backend.Tests/`（`backend.csproj` が `**/*.cs` を取り込むため、`backend/` の外に置く） |
| フォルダ | `tests/backend.Tests/Domain/` |
| フレームワーク | xUnit |
| アサーション | xUnit 標準の `Assert`（FluentAssertions は v8 から商用ライセンスのため使わない） |
| クラス名 | `{対象クラス}Tests`（`UserTests` / `ReservationTests` / `RideGroupTests`） |
| メソッド名 | `{メソッド}_{条件}_{期待結果}`（日本語可。例：`Cancel_状態がMatching_Cancelledになる`） |

### テストダブル

- domain 層は外部依存がないため、**モック・スタブは使わない**。実物のエンティティを生成して検証する。
- 状態遷移のテスト用に、任意の状態の `Reservation` を作るヘルパーをテストクラス内に置く（`Create` → `Confirm` → `StartInProgress` → `Complete` / `Cancel` を必要なところまで呼ぶ）。private なコンストラクタやリフレクションで状態を直接書き換えない。
- `Rider` / `Driver` の `Create` は `internal` で、`InternalsVisibleTo` もないため、`User.Create` / `User.AddRole` を通して検証する（コードは変更しない）。

### 時刻の検証

`DateTime.UtcNow` を直接使っているため、時刻は固定できない。次の方法で検証する。

- 操作の直前と直後に `DateTime.UtcNow` を取り、`Assert.InRange(actual, before, after)` で挟む。
- `Kind` が `DateTimeKind.Utc` であることも確認する。
- 時計の分解能により、連続する操作で同じ値になりうる。`UpdatedAt > CreatedAt` のような **厳密な大小は検証しない**（`>=` で確認する）。

### 例外の検証

- `Assert.Throws<T>` は **型の完全一致** で判定する。`ArgumentOutOfRangeException` は `ArgumentException` の派生だが、`Assert.Throws<ArgumentException>` では失敗するので、期待する型を正確に書く。
- `ArgumentException` は `ParamName` まで確認する（handler が `details[].field` に使っているため）。

### テストの書き方の例

```csharp
public class ReservationTests
{
    public static TheoryData<ReservationStatus> CancelできないStatus =>
        new() { ReservationStatus.InProgress, ReservationStatus.Completed, ReservationStatus.Cancelled };

    [Theory]
    [MemberData(nameof(CancelできないStatus))]
    public void Cancel_キャンセル不可の状態_InvalidOperationExceptionで状態は変わらない(ReservationStatus status)
    {
        var reservation = CreateIn(status);
        var updatedAt = reservation.UpdatedAt;

        Assert.Throws<InvalidOperationException>(() => reservation.Cancel("理由"));

        Assert.Equal(status, reservation.Status);
        Assert.Equal(updatedAt, reservation.UpdatedAt);
    }

    // 公開メソッドだけで任意の状態を作る
    private static Reservation CreateIn(ReservationStatus status)
    {
        var r = Reservation.Create("R-20260926-0001", Guid.NewGuid(), "駅前", "病院",
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        switch (status)
        {
            case ReservationStatus.Confirmed: r.Confirm(); break;
            case ReservationStatus.InProgress: r.Confirm(); r.StartInProgress(); break;
            case ReservationStatus.Completed: r.Confirm(); r.StartInProgress(); r.Complete(); break;
            case ReservationStatus.Cancelled: r.Cancel(null); break;
        }
        return r;
    }
}
```

### この層で書かないこと

- リポジトリ・DB・EF Core のマッピング（Infrastructure 層で扱う）
- 未完了の予約・便があるときのロール切り替え禁止、メールアドレスの重複チェック、予約番号の採番、パスワードのハッシュ化（Usecase 層で扱う）
- HTTP ステータスやエラーレスポンスへの変換（Handler 層で扱う）

## 3. テスト対象一覧

| クラス | メンバー | 種別 | テストクラス |
| --- | --- | --- | --- |
| `User` | `Create(...)` | static ファクトリ | `UserTests` |
| `User` | `FullName` / `KanaFullName` / `Roles` | プロパティ | `UserTests` |
| `User` | `HasRole(UserRole)` | 問い合わせ | `UserTests` |
| `User` | `AddRole(UserRole)` | 操作 | `UserTests` |
| `User` | `SwitchRole(UserRole)` | 操作 | `UserTests` |
| `User` | `EnsureActiveAs(UserRole)` | 検証 | `UserTests` |
| `Rider` | `Create(Guid)`（internal） | `User` 経由で検証 | `UserTests` |
| `Driver` | `Create(Guid)`（internal） | `User` 経由で検証 | `UserTests` |
| `Reservation` | `Create(...)` | static ファクトリ | `ReservationTests` |
| `Reservation` | `Confirm()` | 状態遷移 | `ReservationTests` |
| `Reservation` | `StartInProgress()` | 状態遷移 | `ReservationTests` |
| `Reservation` | `Complete()` | 状態遷移 | `ReservationTests` |
| `Reservation` | `Cancel(string?)` | 状態遷移 | `ReservationTests` |
| `RideGroup` | `Propose(string, Guid)` | static ファクトリ | `RideGroupTests` |

### 予約の状態遷移表

○ = 許可、× = `InvalidOperationException`

| 現在の状態 ＼ 操作 | `Confirm` | `StartInProgress` | `Complete` | `Cancel` |
| --- | --- | --- | --- | --- |
| `Matching` | ○ → `Confirmed` | × | × | ○ → `Cancelled` |
| `Confirmed` | × | ○ → `InProgress` | × | ○ → `Cancelled` |
| `InProgress` | × | × | ○ → `Completed` | × |
| `Completed` | × | × | × | × |
| `Cancelled` | × | × | × | × |

## 4. テストケース

有効な入力の既定値（特に断りがなければこれを使う）

- `User.Create`: email=`taro.yamada@example.com`、passwordHash=`hash`、lastName=`山田`、firstName=`太郎`、kanaLastName=`ヤマダ`、kanaFirstName=`タロウ`
- `Reservation.Create`: reservationNumber=`R-20260926-0001`、userId=任意の Guid、pickupLocation=`駅前`、destination=`病院`、requestedPickupAt=`2026-10-01T00:00:00Z`、passengerCount=`1`
- `RideGroup.Propose`: groupNumber=`G-0001`、driverId=任意の Guid

### 4.1 User（UserTests）

#### User.Create

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-001 | `Create` | 正常系 | 既定値、initialRole=`Rider` | 各プロパティが入力どおり。`Id` が `Guid.Empty` でない。`ActiveRole`=`Rider`、`Rider` が非 null、`Driver` が null | 高 |
| D-002 | `Create` | 正常系 | 既定値、initialRole=`Driver` | `ActiveRole`=`Driver`、`Driver` が非 null、`Rider` が null | 高 |
| D-003 | `Create` | 時刻 | 既定値 | `CreatedAt` が呼び出し前後の `UtcNow` の間、`Kind`=`Utc` | 中 |
| D-004 | `Create` | 正常系 | 既定値で2回生成 | 2つの `Id` が異なる | 低 |
| D-005 | `Create` | 正常系（整形） | email=`" a@example.com "`、passwordHash/lastName/firstName も前後に空白 | 4項目とも前後の空白が除去されて保持される | 中 |
| D-006 | `Create` | 異常系 | email が `null` / `""` / `" "` / `"\t"` | `ArgumentException`、`ParamName`=`email` | 高 |
| D-007 | `Create` | 異常系 | passwordHash が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`passwordHash` | 高 |
| D-008 | `Create` | 異常系 | lastName が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`lastName` | 高 |
| D-009 | `Create` | 異常系 | firstName が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`firstName` | 高 |
| D-010 | `Create` | 異常系（検証順） | email と lastName を両方空にする | `ParamName`=`email`（引数順で最初の不正値が報告される） | 低 |
| D-011 | `Create` | 境界値（カナ・許可） | kanaLastName が `ヤマダ` / `ヴ` / `ー` / `ァ`（範囲の先頭 U+30A1）/ `ヶ`（範囲の末尾 U+30F6）/ `ヤマダー` | 例外なし。値がそのまま保持される | 高 |
| D-012 | `Create` | 異常系（カナ） | kanaLastName が `null` / `""` / `" "` / `やまだ`（ひらがな）/ `ﾔﾏﾀﾞ`（半角）/ `山田` / `Yamada` / `ヤマ ダ`（半角空白）/ `ヤマ　ダ`（全角空白）/ `ヤマ・ダ`（中黒 U+30FB）/ `ヷ`（U+30F7、範囲外）/ `" ヤマダ"`（前後の空白） | `ArgumentException`、`ParamName`=`kanaLastName` | 高 |
| D-013 | `Create` | 異常系（カナ） | kanaFirstName に D-012 の代表値（`null` / `""` / `たろう` / `ﾀﾛｳ`） | `ArgumentException`、`ParamName`=`kanaFirstName` | 高 |
| D-014 | `Create` | 仕様確認（カナの改行） | kanaLastName=`"ヤマダ\n"` | 現状の挙動を確認する（[5. 既知の問題](#5-既知の問題仕様の曖昧な点) の 1 を参照） | 中 |
| D-015 | `Create` | 異常系 | initialRole=`(UserRole)99` | `ArgumentOutOfRangeException`（`ArgumentException` ではない） | 低 |
| D-016 | `Create`（`Rider.Create`） | 正常系 | initialRole=`Rider` | `user.Rider.UserId`=`user.Id`、`user.Rider.CreatedAt` が呼び出し前後の間 | 中 |
| D-017 | `Create`（`Driver.Create`） | 正常系 | initialRole=`Driver` | `user.Driver.UserId`=`user.Id`、`user.Driver.CreatedAt` が呼び出し前後の間 | 中 |

#### プロパティ

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-018 | `FullName` | 正常系 | 既定値 | `山田 太郎`（姓・半角空白・名） | 中 |
| D-019 | `KanaFullName` | 正常系 | 既定値 | `ヤマダ タロウ` | 中 |
| D-020 | `Roles` | 正常系 | initialRole=`Rider` のみ | `[Rider]` | 中 |
| D-021 | `Roles` | 正常系 | `Rider` で作成後 `AddRole(Driver)` | `[Rider, Driver]`（enum の定義順） | 低 |

#### HasRole

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-022 | `HasRole` | 正常系 | `Rider` で作成、`HasRole(Rider)` | `true` | 中 |
| D-023 | `HasRole` | 正常系 | `Rider` で作成、`HasRole(Driver)` | `false` | 中 |
| D-024 | `HasRole` | 異常系 | `HasRole((UserRole)99)` | `false`（例外にならない） | 低 |

#### AddRole

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-025 | `AddRole` | 正常系 | `Rider` で作成、`AddRole(Driver)` | `Driver` が非 null、`Driver.UserId`=`Id`、`HasRole(Driver)`=`true`。`ActiveRole` は `Rider` のまま | 高 |
| D-026 | `AddRole` | 正常系 | `Driver` で作成、`AddRole(Rider)` | `Rider` が非 null、`ActiveRole` は `Driver` のまま | 高 |
| D-027 | `AddRole` | 異常系 | `Rider` で作成、`AddRole(Rider)` | `InvalidOperationException`。`Rider` は元と同じインスタンス（作り直されない） | 高 |
| D-028 | `AddRole` | 異常系 | `Rider` で作成、`AddRole(Driver)` を2回 | 2回目が `InvalidOperationException`。`Driver` は1回目のインスタンスのまま | 中 |
| D-029 | `AddRole` | 異常系 | `AddRole((UserRole)99)` | `ArgumentOutOfRangeException` | 低 |

#### SwitchRole

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-030 | `SwitchRole` | 状態遷移（許可） | 両ロール保有、`ActiveRole`=`Rider`、`SwitchRole(Driver)` | `ActiveRole`=`Driver` | 高 |
| D-031 | `SwitchRole` | 状態遷移（許可） | 両ロール保有、`ActiveRole`=`Driver`、`SwitchRole(Rider)` | `ActiveRole`=`Rider` | 高 |
| D-032 | `SwitchRole` | 状態遷移（同じロール） | `ActiveRole`=`Rider`、`SwitchRole(Rider)` | 例外なし、`ActiveRole`=`Rider` | 中 |
| D-033 | `SwitchRole` | 状態遷移（拒否） | `Rider` のみ保有、`SwitchRole(Driver)` / `Driver` のみ保有、`SwitchRole(Rider)` | `InvalidOperationException`、`ActiveRole` は変わらない | 高 |
| D-034 | `SwitchRole` | 異常系 | `SwitchRole((UserRole)99)` | `InvalidOperationException`、`ActiveRole` は変わらない | 低 |

#### EnsureActiveAs

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-035 | `EnsureActiveAs` | 正常系 | `ActiveRole`=`Rider`、`EnsureActiveAs(Rider)` | 例外なし | 高 |
| D-036 | `EnsureActiveAs` | 異常系 | `ActiveRole`=`Rider`、`EnsureActiveAs(Driver)`（ロールを保有していても稼働していなければ不可） | `InvalidOperationException` | 高 |
| D-037 | `EnsureActiveAs` | 状態遷移との組み合わせ | 両ロール保有、`SwitchRole(Driver)` 後に `EnsureActiveAs(Driver)` と `EnsureActiveAs(Rider)` | 前者は例外なし、後者は `InvalidOperationException` | 中 |

### 4.2 Reservation（ReservationTests）

#### Reservation.Create

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-038 | `Create` | 正常系 | 既定値、considerationNotes=`車いす` | 各プロパティが入力どおり。`Id` が `Guid.Empty` でない。`Status`=`Matching`、`CancellationReason`・`CancelledAt` が null | 高 |
| D-039 | `Create` | 時刻 | 既定値 | `CreatedAt`=`UpdatedAt`、呼び出し前後の間、`Kind`=`Utc` | 中 |
| D-040 | `Create` | 正常系 | considerationNotes を省略 | `ConsiderationNotes` が null | 中 |
| D-041 | `Create` | 境界値 | passengerCount=`1` | 例外なし | 高 |
| D-042 | `Create` | 境界値・異常系 | passengerCount=`0` / `-1` / `int.MinValue` | `ArgumentException`、`ParamName`=`passengerCount` | 高 |
| D-043 | `Create` | 境界値 | passengerCount=`int.MaxValue` | 例外なし（上限チェックはない。5. の 4 を参照） | 低 |
| D-044 | `Create` | 異常系 | reservationNumber が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`reservationNumber` | 高 |
| D-045 | `Create` | 異常系 | pickupLocation が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`pickupLocation` | 高 |
| D-046 | `Create` | 異常系 | destination が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`destination` | 高 |
| D-047 | `Create` | 仕様確認（整形なし） | reservationNumber / pickupLocation / destination / considerationNotes の前後に空白 | 空白を含んだまま保持される（`User` と違い Trim しない） | 低 |
| D-048 | `Create` | 仕様確認（未検証の値） | requestedPickupAt が過去日時・`Kind`=`Unspecified`、userId=`Guid.Empty`、considerationNotes=`""` | 例外なし。値はそのまま保持される（`Kind` も変換されない） | 低 |
| D-049 | `Create` | 異常系（検証順） | reservationNumber と passengerCount を両方不正にする | `ParamName`=`reservationNumber` | 低 |
| D-050 | `Create` | 正常系 | 既定値で2回生成 | 2つの `Id` が異なる | 低 |

#### 状態遷移（許可）

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-051 | `Confirm` | 状態遷移（許可） | `Matching` | `Status`=`Confirmed`。`UpdatedAt` が呼び出し前後の間 | 高 |
| D-052 | `StartInProgress` | 状態遷移（許可） | `Confirmed` | `Status`=`InProgress`。`UpdatedAt` が呼び出し前後の間 | 高 |
| D-053 | `Complete` | 状態遷移（許可） | `InProgress` | `Status`=`Completed`。`UpdatedAt` が呼び出し前後の間 | 高 |
| D-054 | `Cancel` | 状態遷移（許可） | `Matching`、reason=`予定が変わったため` | `Status`=`Cancelled`、`CancellationReason`=入力値、`CancelledAt`・`UpdatedAt` が呼び出し前後の間 | 高 |
| D-055 | `Cancel` | 状態遷移（許可） | `Confirmed`、reason=`予定が変わったため` | D-054 と同じ | 高 |
| D-056 | `Cancel` | 正常系 | `Matching`、reason=`null` | `Status`=`Cancelled`、`CancellationReason` が null、`CancelledAt` は設定される | 中 |
| D-057 | `Cancel` | 境界値 | `Matching`、reason=`""` / `" "` | 例外なし。値がそのまま保持される（5. の 5 を参照） | 低 |
| D-058 | 全遷移メソッド | 不変項目 | D-051〜D-055 の各遷移 | `Id`・`ReservationNumber`・`UserId`・乗車地・目的地・希望日時・人数・配慮事項・`CreatedAt` が変わらない。`UpdatedAt >= CreatedAt` | 低 |
| D-059 | `Confirm` → `StartInProgress` → `Complete` | 状態遷移（一連） | `Matching` から順に呼ぶ | 最終的に `Completed`。各段階の `UpdatedAt` が前の値以上 | 中 |

#### 状態遷移（拒否）

期待結果はすべて `InvalidOperationException`。ケースは `[Theory]` でまとめてよい。

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-060 | `Confirm` | 状態遷移（拒否） | `Confirmed` | `InvalidOperationException` | 高 |
| D-061 | `Confirm` | 状態遷移（拒否） | `InProgress` | `InvalidOperationException` | 中 |
| D-062 | `Confirm` | 状態遷移（拒否） | `Completed` | `InvalidOperationException` | 中 |
| D-063 | `Confirm` | 状態遷移（拒否） | `Cancelled` | `InvalidOperationException` | 高 |
| D-064 | `StartInProgress` | 状態遷移（拒否） | `Matching`（確定を飛ばす） | `InvalidOperationException` | 高 |
| D-065 | `StartInProgress` | 状態遷移（拒否） | `InProgress` | `InvalidOperationException` | 中 |
| D-066 | `StartInProgress` | 状態遷移（拒否） | `Completed` | `InvalidOperationException` | 中 |
| D-067 | `StartInProgress` | 状態遷移（拒否） | `Cancelled` | `InvalidOperationException` | 高 |
| D-068 | `Complete` | 状態遷移（拒否） | `Matching` | `InvalidOperationException` | 高 |
| D-069 | `Complete` | 状態遷移（拒否） | `Confirmed`（運行開始を飛ばす） | `InvalidOperationException` | 高 |
| D-070 | `Complete` | 状態遷移（拒否） | `Completed` | `InvalidOperationException` | 中 |
| D-071 | `Complete` | 状態遷移（拒否） | `Cancelled` | `InvalidOperationException` | 高 |
| D-072 | `Cancel` | 状態遷移（拒否） | `InProgress` | `InvalidOperationException` | 高 |
| D-073 | `Cancel` | 状態遷移（拒否） | `Completed` | `InvalidOperationException` | 高 |
| D-074 | `Cancel` | 状態遷移（拒否） | `Cancelled`（二重キャンセル） | `InvalidOperationException`。1回目の `CancellationReason`・`CancelledAt` が上書きされない | 高 |
| D-075 | 全遷移メソッド | 拒否時の副作用なし | D-060〜D-074 の全15通り | `Status`・`UpdatedAt`・`CancellationReason`・`CancelledAt` が呼び出し前と同じ | 高 |
| D-076 | 全遷移メソッド | メッセージ | D-060〜D-074 の全15通り | `Message` に現在の状態名（例：`Cancelled`）が含まれる（API のエラーメッセージにそのまま出るため） | 低 |

### 4.3 RideGroup（RideGroupTests）

| ID | 対象メソッド | 観点 | 前提・入力 | 期待結果 | 優先度 |
| --- | --- | --- | --- | --- | --- |
| D-077 | `Propose` | 正常系 | 既定値 | `Status`=`Proposed`、`GroupNumber`・`DriverId` が入力どおり、`Id` が `Guid.Empty` でない | 高 |
| D-078 | `Propose` | 時刻 | 既定値 | `CreatedAt`=`UpdatedAt`、呼び出し前後の間、`Kind`=`Utc` | 中 |
| D-079 | `Propose` | 異常系 | groupNumber が `null` / `""` / `" "` | `ArgumentException`、`ParamName`=`groupNumber` | 高 |
| D-080 | `Propose` | 仕様確認（整形なし） | groupNumber=`" G-0001 "` | 空白を含んだまま保持される | 低 |
| D-081 | `Propose` | 仕様確認（未検証の値） | driverId=`Guid.Empty` | 例外なし | 低 |
| D-082 | `Propose` | 正常系 | 既定値で2回生成 | 2つの `Id` が異なる | 低 |

テストケース数：82 件（`[Theory]` の入力パターンは1件として数える）

## 5. 既知の問題・仕様の曖昧な点

実装を読んで気づいた点です。仕様として正しいかは未確定のものを含むため、テストでは **現状の挙動を確認するテスト** として書き、仕様が決まったら期待値を見直す。

| # | 内容 | 関連ケース |
| --- | --- | --- |
| 1 | カナの正規表現 `^[ァ-ヶー]+$` は `RegexOptions.Multiline` なしでも、.NET の `$` が「末尾の改行の直前」にも一致する。そのため `"ヤマダ\n"` が通る可能性が高い。`\z` を使うべきかは要確認。テストでは実際の挙動を確認して記録する | D-014 |
| 2 | 空白の扱いが統一されていない。`User` の email・氏名は Trim するが、カナは Trim せず前後の空白で拒否する。`Reservation` と `RideGroup` の文字列は Trim しない | D-005, D-012, D-047, D-080 |
| 3 | 文字数の上限（姓名 50、乗車地 200、配慮事項 500 など）は domain でチェックしていない（ENDPOINT.md「既知の制約」と同じ）。domain のテストでは上限を検証しない | － |
| 4 | `passengerCount` は下限（1）のみで上限がない。REQUIREMENTS.md FR-R04 は「車両定員以内」だが、定員は便（車両）が決まるまで分からないため、どの層で検証するかは未定 | D-043 |
| 5 | `Cancel` の理由は未検証。`null`・空文字・空白のみをそのまま保存する。空文字を `null` に寄せるべきかは未定 | D-056, D-057 |
| 6 | `Cancel` は `CancelledAt` と `UpdatedAt` に別々の `DateTime.UtcNow` を代入しているため、両者がわずかにずれる可能性がある。テストで `CancelledAt == UpdatedAt` を検証しない | D-054, D-055 |
| 7 | `requestedPickupAt` の過去日時や `DateTimeKind` を検証・変換していない。UTC への変換は handler 側の JSON 変換に依存している | D-048 |
| 8 | `userId` / `driverId` の `Guid.Empty`、email の形式、`Reservation` の `userId` が利用者ロールを持つかは domain で検証していない | D-048, D-081 |
| 9 | `Reservation.Confirm` / `StartInProgress` / `Complete` と `RideGroup.Propose` は、現時点で usecase から呼ばれていない。テストは domain 単体の仕様として書く | D-051〜D-053, D-077 |
| 10 | `RideGroup` は `Propose` だけで、`Confirmed` 以降への遷移メソッドがない。REQUIREMENTS.md 7.2 の状態遷移は未実装。DATA_MODEL.md の `vehicle_id`・`confirmed_at` などの項目も domain にない | 6. 対象外 |
| 11 | 未定義の enum 値を渡したときの例外が操作ごとに違う（`AddRole`・`Create` は `ArgumentOutOfRangeException`、`SwitchRole` は `InvalidOperationException`、`HasRole` は `false`）。handler 側で 422 と 409 のどちらになるかに影響する可能性がある | D-015, D-024, D-029, D-034 |
| 12 | `User.Create` で initialRole が不正な場合、`User` を組み立てた後の `AddRole` で例外になる。他の入力検証より後に判定される | D-015 |

## 6. 対象外

| 対象 | 理由 |
| --- | --- |
| 各クラスの private コンストラクタ | EF Core が DB からの復元に使うもので、公開 API ではない。復元の正しさは Infrastructure 層で確認する |
| `Rider.Create` / `Driver.Create` の直接呼び出し | `internal` で、`User` 集約を通してのみ生成する設計のため。`User` 経由で検証する（D-016, D-017, D-025, D-026） |
| 未完了の予約・便があるときのロール切り替え禁止 | usecase 層（`User.cs` の切り替えユースケース）で判定しているため |
| メールアドレス・予約番号・便番号の一意性 | DB 制約と usecase 層で扱うため |
| 予約番号の採番形式 | usecase 層（`GenerateReservationNumber`）の責務のため |
| パスワードのハッシュ化・照合 | usecase / infrastructure の責務。domain はハッシュ済みの文字列を受け取るだけ |
| 文字数の上限 | domain に実装がなく、DB 制約で担保しているため（5. の 3） |
| `RideGroup` の `Proposed` 以降の状態遷移 | domain に実装がないため（5. の 10）。実装されたら本書に追加する |
| enum と DB 文字列（`in_progress` など）の相互変換 | `AppDbContext` の ValueConverter で行っており、Infrastructure 層で扱う |
| 例外から HTTP ステータス（409 / 422）への変換 | Handler 層で扱う |
