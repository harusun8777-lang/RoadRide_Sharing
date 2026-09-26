using Backend.Tests.Infrastructure.Fixtures;
using Backend.Tests.TestDoubles;
using Domain.Reservations;
using Domain.RideGroups;
using Domain.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Infrastructure;

public partial class AppDbContextTests
{
    // DB 上の挙動（スキーマ作成・値変換・UTC 変換・制約）
    [Collection(SqlServerCollection.Name)]
    [Trait("Category", "Database")]
    public class Database(SqlServerFixture fixture) : DatabaseTestBase(fixture)
    {
        private async Task<string[]> QueryStringsAsync(FormattableString sql)
        {
            await using var db = CreateContext();
            return (await db.Database.SqlQuery<string>(sql).ToListAsync()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private async Task<DateTime> QueryRequestedPickupAtAsync(Guid id)
        {
            await using var db = CreateContext();
            return await db.Database
                .SqlQuery<DateTime>($"SELECT requested_pickup_at AS Value FROM reservations WHERE id = {id}")
                .SingleAsync();
        }

        private async Task<Reservation> ReloadReservationAsync(Guid id)
        {
            await using var db = CreateContext();
            return await db.Reservations.SingleAsync(r => r.Id == id);
        }

        // I-059
        [Fact]
        public async Task EnsureCreated_空のDB_テーブルと一意インデックスとCHECK制約が作られる()
        {
            var tables = await QueryStringsAsync(
                $"SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
            Assert.Equal(["drivers", "reservations", "ride_groups", "riders", "users"], tables);

            var indexes = await QueryStringsAsync($"""
                SELECT OBJECT_NAME(i.object_id) + '.' + c.name + ':' + CASE i.is_unique WHEN 1 THEN 'unique' ELSE 'non-unique' END AS Value
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.is_primary_key = 0 AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
                """);
            Assert.Contains("users.email:unique", indexes);
            Assert.Contains("ride_groups.group_number:unique", indexes);
            Assert.Contains("reservations.reservation_number:unique", indexes);
            Assert.Contains("reservations.requested_pickup_at:non-unique", indexes);

            var checks = await QueryStringsAsync(
                $"SELECT OBJECT_NAME(parent_object_id) + '.' + name AS Value FROM sys.check_constraints");
            Assert.Equal(["reservations.CK_reservations_passenger_count"], checks);
        }

        // I-060
        [Fact]
        public async Task 前提の確認_テスト用DB_照合順序が大文字小文字を区別しない()
        {
            var collation = Assert.Single(await QueryStringsAsync(
                $"SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128)) AS Value"));

            Assert.Contains("_CI_", collation);
        }

        // I-061
        [Fact]
        public async Task 値変換_区分と状態_小文字の文字列で保存される()
        {
            var user = await SaveUserAsync(TestUsers.Rider());
            await SaveReservationAsync(NewReservation(user.Id, Utc(2026, 10, 1)));

            Assert.Equal(["rider"], await QueryStringsAsync($"SELECT active_role AS Value FROM users"));
            Assert.Equal(["matching"], await QueryStringsAsync($"SELECT status AS Value FROM reservations"));
        }

        // I-062
        [Fact]
        public async Task UTC変換_Utcの値_TicksとKindが往復で一致する()
        {
            var user = await SaveUserAsync(TestUsers.Rider());
            var value = Utc(2026, 10, 1, 9, 30);
            var saved = await SaveReservationAsync(NewReservation(user.Id, value));

            var read = await ReloadReservationAsync(saved.Id);

            Assert.Equal(value.Ticks, read.RequestedPickupAt.Ticks);
            Assert.Equal(DateTimeKind.Utc, read.RequestedPickupAt.Kind);
        }

        // I-063
        // 期待値は ToUniversalTime() で求めるのでどのタイムゾーンでも通る。
        // ホストが UTC だと変換の有無を区別できない（設計書 5 章 #5）
        [Fact]
        public async Task UTC変換_Localの値_UTCに変換して保存し読み出すとUtc()
        {
            var user = await SaveUserAsync(TestUsers.Rider());
            var value = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Local);
            var saved = await SaveReservationAsync(NewReservation(user.Id, value));

            Assert.Equal(value.ToUniversalTime().Ticks, (await QueryRequestedPickupAtAsync(saved.Id)).Ticks);
            var read = await ReloadReservationAsync(saved.Id);
            Assert.Equal(value.ToUniversalTime().Ticks, read.RequestedPickupAt.Ticks);
            Assert.Equal(DateTimeKind.Utc, read.RequestedPickupAt.Kind);
        }

        // I-064
        [Fact]
        public async Task UTC変換_Unspecifiedの値_そのまま保存されUTCとして読まれる_現状の挙動()
        {
            var user = await SaveUserAsync(TestUsers.Rider());
            var value = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Unspecified);
            var saved = await SaveReservationAsync(NewReservation(user.Id, value));

            Assert.Equal(value.Ticks, (await QueryRequestedPickupAtAsync(saved.Id)).Ticks);
            var read = await ReloadReservationAsync(saved.Id);
            Assert.Equal(value.Ticks, read.RequestedPickupAt.Ticks);
            Assert.Equal(DateTimeKind.Utc, read.RequestedPickupAt.Kind);
        }

        // I-065
        [Fact]
        public async Task UTC変換_100ナノ秒単位の値_丸められずTicksが一致する()
        {
            var user = await SaveUserAsync(TestUsers.Rider());
            var value = Utc(2026, 10, 1, 9, 30).AddTicks(1234567);
            var saved = await SaveReservationAsync(NewReservation(user.Id, value));

            var read = await ReloadReservationAsync(saved.Id);

            Assert.Equal(value.Ticks, read.RequestedPickupAt.Ticks);
        }

        // I-066
        [Fact]
        public async Task UTC変換_全DateTime列_読み出すとすべてKindがUtc()
        {
            var user = await SaveUserAsync(TestUsers.Both(UserRole.Rider));
            var reservation = NewReservation(user.Id, Utc(2026, 10, 1));
            reservation.Cancel("テスト");
            await SaveReservationAsync(reservation);
            var group = await SaveRideGroupAsync(user.Id);

            await using var db = CreateContext();
            var readUser = await db.Users.Include(u => u.Rider).Include(u => u.Driver).SingleAsync(u => u.Id == user.Id);
            var readReservation = await db.Reservations.SingleAsync(r => r.Id == reservation.Id);
            var readGroup = await db.RideGroups.SingleAsync(g => g.Id == group.Id);

            DateTime[] values =
            [
                readUser.CreatedAt, readUser.Rider!.CreatedAt, readUser.Driver!.CreatedAt,
                readReservation.CreatedAt, readReservation.UpdatedAt, readReservation.CancelledAt!.Value,
                readGroup.CreatedAt, readGroup.UpdatedAt
            ];
            Assert.All(values, v => Assert.Equal(DateTimeKind.Utc, v.Kind));
            Assert.Equal(user.CreatedAt.Ticks, readUser.CreatedAt.Ticks);
            Assert.Equal(reservation.CancelledAt!.Value.Ticks, readReservation.CancelledAt.Value.Ticks);
            Assert.Equal(group.UpdatedAt.Ticks, readGroup.UpdatedAt.Ticks);
        }

        // 最大長のテスト用に、指定した列だけ長さを変えた行を保存し、読み直した値を返す
        private async Task<string> SaveWithLengthAsync(string column, int length)
        {
            var katakana = new string('ア', length);
            string Text(char c = 'a') => new(c, length);

            switch (column)
            {
                case "email":
                case "password_hash":
                case "last_name":
                case "first_name":
                case "kana_last_name":
                case "kana_first_name":
                {
                    var email = column == "email" ? new string('a', length - "@example.com".Length) + "@example.com" : $"u{Guid.NewGuid():N}@example.com";
                    var user = DomainUser.Create(
                        email,
                        column == "password_hash" ? Text() : "hash",
                        column == "last_name" ? Text() : "山田",
                        column == "first_name" ? Text() : "太郎",
                        column == "kana_last_name" ? katakana : "ヤマダ",
                        column == "kana_first_name" ? katakana : "タロウ",
                        UserRole.Rider);
                    await SaveUserAsync(user);
                    await using var db = CreateContext();
                    var read = await db.Users.SingleAsync(u => u.Id == user.Id);
                    return column switch
                    {
                        "email" => read.Email,
                        "password_hash" => read.PasswordHash,
                        "last_name" => read.LastName,
                        "first_name" => read.FirstName,
                        "kana_last_name" => read.KanaLastName,
                        _ => read.KanaFirstName
                    };
                }
                case "group_number":
                {
                    var driver = await SaveUserAsync(TestUsers.Driver());
                    var group = await SaveRideGroupAsync(driver.Id, groupNumber: Text('G'));
                    await using var db = CreateContext();
                    return (await db.RideGroups.SingleAsync(g => g.Id == group.Id)).GroupNumber;
                }
                default:
                {
                    var rider = await SaveUserAsync(TestUsers.Rider());
                    var reservation = Reservation.Create(
                        column == "reservation_number" ? Text('R') : NewNumber("RR"),
                        rider.Id,
                        column == "pickup_location" ? Text() : "市役所前",
                        column == "destination" ? Text() : "中央病院",
                        Utc(2026, 10, 1),
                        1,
                        column == "consideration_notes" ? Text() : null);
                    if (column == "cancellation_reason")
                        reservation.Cancel(Text());
                    await SaveReservationAsync(reservation);
                    var read = await ReloadReservationAsync(reservation.Id);
                    return column switch
                    {
                        "reservation_number" => read.ReservationNumber,
                        "pickup_location" => read.PickupLocation,
                        "destination" => read.Destination,
                        "consideration_notes" => read.ConsiderationNotes!,
                        _ => read.CancellationReason!
                    };
                }
            }
        }

        // 利用者の入力やアプリで値を決められる文字列列。active_role / status は値変換で決まるため除く
        public static TheoryData<string, int> StringColumns => new()
        {
            { "email", 254 },
            { "password_hash", 255 },
            { "last_name", 50 },
            { "first_name", 50 },
            { "kana_last_name", 50 },
            { "kana_first_name", 50 },
            { "group_number", 32 },
            { "reservation_number", 32 },
            { "pickup_location", 200 },
            { "destination", 200 },
            { "consideration_notes", 500 },
            { "cancellation_reason", 500 },
        };

        // I-067
        [Theory]
        [MemberData(nameof(StringColumns))]
        public async Task 最大長_最大長ちょうどの値_保存でき読み直して一致する(string column, int maxLength)
        {
            var read = await SaveWithLengthAsync(column, maxLength);

            Assert.Equal(maxLength, read.Length);
        }

        // I-068
        [Theory]
        [MemberData(nameof(StringColumns))]
        public async Task 最大長_1文字超過_DbUpdateExceptionで2628(string column, int maxLength)
        {
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveWithLengthAsync(column, maxLength + 1));

            Assert.Equal(2628, SqlErrorNumber(ex));
        }

        // I-069
        [Fact]
        public async Task 最大長_日本語200文字の乗車地_文字化けせず読み直せる()
        {
            var rider = await SaveUserAsync(TestUsers.Rider());
            var pickup = string.Concat(Enumerable.Repeat("東京都千代田区丸の内", 20));
            var reservation = Reservation.Create(NewNumber("RR"), rider.Id, pickup, "中央病院", Utc(2026, 10, 1), 1);
            await SaveReservationAsync(reservation);

            Assert.Equal(200, pickup.Length);
            Assert.Equal(pickup, (await ReloadReservationAsync(reservation.Id)).PickupLocation);
        }

        // I-070
        [Theory]
        [InlineData(25, true)]  // UTF-16 で 50
        [InlineData(26, false)] // UTF-16 で 52
        public async Task 最大長_サロゲートペアの姓_UTF16の長さで数えられる(int count, bool saved)
        {
            var lastName = string.Concat(Enumerable.Repeat("𠮷", count));
            var user = DomainUser.Create($"u{Guid.NewGuid():N}@example.com", "hash", lastName, "太郎", "ヤマダ", "タロウ", UserRole.Rider);

            if (saved)
            {
                await SaveUserAsync(user);
                await using var db = CreateContext();
                Assert.Equal(lastName, (await db.Users.SingleAsync(u => u.Id == user.Id)).LastName);
            }
            else
            {
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveUserAsync(user));
                Assert.Equal(2628, SqlErrorNumber(ex));
            }
        }

        // I-071
        [Fact]
        public async Task CHECK制約_乗車人数0_SqlExceptionで547()
        {
            var rider = await SaveUserAsync(TestUsers.Rider());
            var now = DateTime.UtcNow;

            await using var db = CreateContext();
            var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync($"""
                INSERT INTO reservations (id, reservation_number, user_id, pickup_location, destination, requested_pickup_at,
                    passenger_count, status, created_at, updated_at)
                VALUES ({Guid.NewGuid()}, {NewNumber("RR")}, {rider.Id}, N'市役所前', N'中央病院', {now}, 0, 'matching', {now}, {now})
                """));

            Assert.Equal(547, ex.Number);
        }

        // I-072
        [Fact]
        public async Task 値変換_DBに不明な状態_読むと例外になる()
        {
            var rider = await SaveUserAsync(TestUsers.Rider());
            var reservation = await SaveReservationAsync(NewReservation(rider.Id, Utc(2026, 10, 1)));
            await using (var update = CreateContext())
                await update.Database.ExecuteSqlAsync($"UPDATE reservations SET status = 'unknown'");

            var ex = await Record.ExceptionAsync(() => ReloadReservationAsync(reservation.Id));

            Assert.IsType<ArgumentOutOfRangeException>(ex);
        }

        // I-073
        [Fact]
        public async Task 値変換_DBに不明な区分_例外にならずRiderになる_現状の挙動()
        {
            var user = await SaveUserAsync(TestUsers.Both(UserRole.Driver));
            await using (var update = CreateContext())
                await update.Database.ExecuteSqlAsync($"UPDATE users SET active_role = 'admin'");

            await using var db = CreateContext();
            var read = await db.Users.SingleAsync(u => u.Id == user.Id);

            Assert.Equal(UserRole.Rider, read.ActiveRole);
        }

        // I-074
        [Fact]
        public async Task 一意制約_便番号の重複_DbUpdateExceptionで2601()
        {
            var driver = await SaveUserAsync(TestUsers.Driver());
            await SaveRideGroupAsync(driver.Id, groupNumber: "RG-DUP");

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveRideGroupAsync(driver.Id, groupNumber: "RG-DUP"));

            Assert.Equal(2601, SqlErrorNumber(ex));
        }

        // I-075
        [Fact]
        public async Task 外部キー_運転手でないユーザーの便_DbUpdateExceptionで547()
        {
            var rider = await SaveUserAsync(TestUsers.Rider());

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveRideGroupAsync(rider.Id));

            Assert.Equal(547, SqlErrorNumber(ex));
        }
    }
}
