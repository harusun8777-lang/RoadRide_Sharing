using Domain.Reservations;
using Domain.RideGroups;
using Domain.Users;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Infrastructure;

public partial class AppDbContextTests
{
    // モデル定義と値変換（DB 不要。モデル構築だけなら接続しない）
    public class Model
    {
        // CHECK 制約は実行時モデルから除かれるため、設計時モデルを読む
        private static readonly IModel DesignModel = CreateDesignModel();

        private static IModel CreateDesignModel()
        {
            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused").Options);
            return db.GetService<IDesignTimeModel>().Model;
        }

        private static IEntityType Entity(Type type) => DesignModel.FindEntityType(type)!;

        private static IProperty Property(Type type, string name) => Entity(type).FindProperty(name)!;

        private static ValueConverter Converter(Type type, string name) => Property(type, name).GetValueConverter()!;

        private static readonly Type[] EntityTypes =
            [typeof(DomainUser), typeof(Rider), typeof(Driver), typeof(RideGroup), typeof(Reservation)];

        // I-042
        [Theory]
        [InlineData(typeof(DomainUser), "users")]
        [InlineData(typeof(Rider), "riders")]
        [InlineData(typeof(Driver), "drivers")]
        [InlineData(typeof(RideGroup), "ride_groups")]
        [InlineData(typeof(Reservation), "reservations")]
        public void OnModelCreating_エンティティ_テーブル名がスネークケース(Type type, string table)
        {
            Assert.Equal(table, Entity(type).GetTableName());
        }

        // I-043
        [Theory]
        [InlineData(typeof(DomainUser), "Id", "id")]
        [InlineData(typeof(DomainUser), "Email", "email")]
        [InlineData(typeof(DomainUser), "PasswordHash", "password_hash")]
        [InlineData(typeof(DomainUser), "LastName", "last_name")]
        [InlineData(typeof(DomainUser), "FirstName", "first_name")]
        [InlineData(typeof(DomainUser), "KanaLastName", "kana_last_name")]
        [InlineData(typeof(DomainUser), "KanaFirstName", "kana_first_name")]
        [InlineData(typeof(DomainUser), "ActiveRole", "active_role")]
        [InlineData(typeof(DomainUser), "CreatedAt", "created_at")]
        [InlineData(typeof(Rider), "UserId", "user_id")]
        [InlineData(typeof(Rider), "CreatedAt", "created_at")]
        [InlineData(typeof(Driver), "UserId", "user_id")]
        [InlineData(typeof(Driver), "CreatedAt", "created_at")]
        [InlineData(typeof(RideGroup), "Id", "id")]
        [InlineData(typeof(RideGroup), "GroupNumber", "group_number")]
        [InlineData(typeof(RideGroup), "DriverId", "driver_id")]
        [InlineData(typeof(RideGroup), "Status", "status")]
        [InlineData(typeof(RideGroup), "CreatedAt", "created_at")]
        [InlineData(typeof(RideGroup), "UpdatedAt", "updated_at")]
        [InlineData(typeof(Reservation), "Id", "id")]
        [InlineData(typeof(Reservation), "ReservationNumber", "reservation_number")]
        [InlineData(typeof(Reservation), "UserId", "user_id")]
        [InlineData(typeof(Reservation), "PickupLocation", "pickup_location")]
        [InlineData(typeof(Reservation), "Destination", "destination")]
        [InlineData(typeof(Reservation), "RequestedPickupAt", "requested_pickup_at")]
        [InlineData(typeof(Reservation), "PassengerCount", "passenger_count")]
        [InlineData(typeof(Reservation), "ConsiderationNotes", "consideration_notes")]
        [InlineData(typeof(Reservation), "Status", "status")]
        [InlineData(typeof(Reservation), "CancellationReason", "cancellation_reason")]
        [InlineData(typeof(Reservation), "CancelledAt", "cancelled_at")]
        [InlineData(typeof(Reservation), "CreatedAt", "created_at")]
        [InlineData(typeof(Reservation), "UpdatedAt", "updated_at")]
        public void OnModelCreating_プロパティ_列名がスネークケース(Type type, string property, string column)
        {
            Assert.Equal(column, Property(type, property).GetColumnName());
        }

        // I-043（表にない列がマップされていないこと）
        [Fact]
        public void OnModelCreating_全エンティティ_列の数が表と一致()
        {
            Assert.Equal(9, Entity(typeof(DomainUser)).GetProperties().Count());
            Assert.Equal(2, Entity(typeof(Rider)).GetProperties().Count());
            Assert.Equal(2, Entity(typeof(Driver)).GetProperties().Count());
            Assert.Equal(6, Entity(typeof(RideGroup)).GetProperties().Count());
            Assert.Equal(13, Entity(typeof(Reservation)).GetProperties().Count());
        }

        // I-044
        [Theory]
        [InlineData(typeof(DomainUser), "Email", 254)]
        [InlineData(typeof(DomainUser), "PasswordHash", 255)]
        [InlineData(typeof(DomainUser), "LastName", 50)]
        [InlineData(typeof(DomainUser), "FirstName", 50)]
        [InlineData(typeof(DomainUser), "KanaLastName", 50)]
        [InlineData(typeof(DomainUser), "KanaFirstName", 50)]
        [InlineData(typeof(DomainUser), "ActiveRole", 20)]
        [InlineData(typeof(RideGroup), "GroupNumber", 32)]
        [InlineData(typeof(RideGroup), "Status", 20)]
        [InlineData(typeof(Reservation), "ReservationNumber", 32)]
        [InlineData(typeof(Reservation), "PickupLocation", 200)]
        [InlineData(typeof(Reservation), "Destination", 200)]
        [InlineData(typeof(Reservation), "ConsiderationNotes", 500)]
        [InlineData(typeof(Reservation), "Status", 20)]
        [InlineData(typeof(Reservation), "CancellationReason", 500)]
        public void OnModelCreating_文字列プロパティ_最大長が表と一致(Type type, string property, int maxLength)
        {
            Assert.Equal(maxLength, Property(type, property).GetMaxLength());
        }

        // I-045
        [Fact]
        public void OnModelCreating_全プロパティ_配慮事項とキャンセル理由とキャンセル日時だけNULL可()
        {
            var nullable = EntityTypes.Select(Entity)
                .SelectMany(t => t.GetProperties().Select(p => (Entity: t, Property: p)))
                .Where(x => x.Property.IsNullable)
                .Select(x => $"{x.Entity.ClrType.Name}.{x.Property.Name}")
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(
                ["Reservation.CancellationReason", "Reservation.CancelledAt", "Reservation.ConsiderationNotes"],
                nullable);
        }

        // I-046
        [Fact]
        public void OnModelCreating_インデックス_一意と非一意が表と一致()
        {
            string Describe(Type type) => string.Join(",", Entity(type).GetIndexes()
                .Select(i => $"{string.Join("+", i.Properties.Select(p => p.Name))}:{(i.IsUnique ? "unique" : "non-unique")}")
                .OrderBy(x => x));

            Assert.Equal("Email:unique", Describe(typeof(DomainUser)));
            // DriverId は外部キー用のインデックス
            Assert.Contains("GroupNumber:unique", Describe(typeof(RideGroup)).Split(','));
            var reservationIndexes = Describe(typeof(Reservation)).Split(',');
            Assert.Contains("ReservationNumber:unique", reservationIndexes);
            Assert.Contains("RequestedPickupAt:non-unique", reservationIndexes);
        }

        // I-047
        [Theory]
        [InlineData(typeof(DomainUser), "Id")]
        [InlineData(typeof(Rider), "UserId")]
        [InlineData(typeof(Driver), "UserId")]
        [InlineData(typeof(RideGroup), "Id")]
        [InlineData(typeof(Reservation), "Id")]
        public void OnModelCreating_主キー_アプリ側で生成しDBでは生成しない(Type type, string key)
        {
            var pk = Entity(type).FindPrimaryKey()!;

            Assert.Equal(key, Assert.Single(pk.Properties).Name);
            Assert.Equal(ValueGenerated.Never, pk.Properties[0].ValueGenerated);
        }

        // I-048
        [Theory]
        [InlineData("FullName")]
        [InlineData("KanaFullName")]
        [InlineData("Roles")]
        public void OnModelCreating_計算プロパティ_マップされていない(string name)
        {
            var user = Entity(typeof(DomainUser));

            Assert.Null(user.FindProperty(name));
            Assert.Null(user.FindNavigation(name));
        }

        // I-049
        [Theory]
        [InlineData(typeof(Rider), "UserId", "users", "id", DeleteBehavior.Cascade)]
        [InlineData(typeof(Driver), "UserId", "users", "id", DeleteBehavior.Cascade)]
        [InlineData(typeof(RideGroup), "DriverId", "drivers", "user_id", DeleteBehavior.Restrict)]
        [InlineData(typeof(Reservation), "UserId", "riders", "user_id", DeleteBehavior.Restrict)]
        public void OnModelCreating_外部キー_参照先と削除動作が表と一致(Type type, string property, string principalTable, string principalColumn, DeleteBehavior deleteBehavior)
        {
            var fk = Assert.Single(Entity(type).GetForeignKeys());

            Assert.Equal(property, Assert.Single(fk.Properties).Name);
            Assert.Equal(principalTable, fk.PrincipalEntityType.GetTableName());
            Assert.Equal(principalColumn, Assert.Single(fk.PrincipalKey.Properties).GetColumnName());
            Assert.Equal(deleteBehavior, fk.DeleteBehavior);
        }

        // I-049（外部キーの数）
        [Fact]
        public void OnModelCreating_ユーザー_外部キーを持たない()
        {
            Assert.Empty(Entity(typeof(DomainUser)).GetForeignKeys());
        }

        // I-050
        [Fact]
        public void OnModelCreating_予約_乗車人数のCHECK制約がある()
        {
            var check = Assert.Single(Entity(typeof(Reservation)).GetCheckConstraints());

            Assert.Equal("CK_reservations_passenger_count", check.ModelName);
            Assert.Equal("[passenger_count] >= 1", check.Sql);
        }

        // I-051
        [Theory]
        [InlineData(UserRole.Rider, "rider")]
        [InlineData(UserRole.Driver, "driver")]
        public void 値変換UserRole_区分_小文字の文字列と相互に変換できる(UserRole role, string value)
        {
            var converter = Converter(typeof(DomainUser), "ActiveRole");

            Assert.Equal(value, converter.ConvertToProvider(role));
            Assert.Equal(role, converter.ConvertFromProvider(value));
        }

        // I-052
        [Theory]
        [InlineData("admin")]
        [InlineData("Driver")]
        public void 値変換UserRole_不明な文字列_Riderになる_現状の挙動(string value)
        {
            var converter = Converter(typeof(DomainUser), "ActiveRole");

            // "driver" 以外はすべて Rider として読む（設計書 5 章 #11）
            Assert.Equal(UserRole.Rider, converter.ConvertFromProvider(value));
        }

        // I-053
        [Theory]
        [InlineData(ReservationStatus.Matching, "matching")]
        [InlineData(ReservationStatus.Confirmed, "confirmed")]
        [InlineData(ReservationStatus.InProgress, "in_progress")]
        [InlineData(ReservationStatus.Completed, "completed")]
        [InlineData(ReservationStatus.Cancelled, "cancelled")]
        public void 値変換ReservationStatus_状態_文字列と相互に変換できる(ReservationStatus status, string value)
        {
            var converter = Converter(typeof(Reservation), "Status");

            Assert.Equal(value, converter.ConvertToProvider(status));
            Assert.Equal(status, converter.ConvertFromProvider(value));
        }

        // I-054
        [Theory]
        [InlineData(RideGroupStatus.Proposed, "proposed")]
        [InlineData(RideGroupStatus.Confirmed, "confirmed")]
        [InlineData(RideGroupStatus.InProgress, "in_progress")]
        [InlineData(RideGroupStatus.Completed, "completed")]
        [InlineData(RideGroupStatus.Cancelled, "cancelled")]
        public void 値変換RideGroupStatus_状態_文字列と相互に変換できる(RideGroupStatus status, string value)
        {
            var converter = Converter(typeof(RideGroup), "Status");

            Assert.Equal(value, converter.ConvertToProvider(status));
            Assert.Equal(status, converter.ConvertFromProvider(value));
        }

        // I-055
        [Theory]
        [InlineData(typeof(Reservation))]
        [InlineData(typeof(RideGroup))]
        public void 値変換状態_不明な値_ArgumentOutOfRangeException(Type type)
        {
            var converter = Converter(type, "Status");
            object outOfRange = type == typeof(Reservation) ? (ReservationStatus)99 : (RideGroupStatus)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => converter.ConvertFromProvider("unknown"));
            Assert.Throws<ArgumentOutOfRangeException>(() => converter.ConvertToProvider(outOfRange));
        }

        // I-056
        // Local の期待値はホストのタイムゾーンで決まるため ToUniversalTime() で求める。
        // ホストが UTC だと Ticks は変わらないが、Kind が Utc になることは確かめられる
        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void 値変換DateTime_保存_LocalだけUTCに変換される(DateTimeKind kind)
        {
            var converter = Converter(typeof(Reservation), "RequestedPickupAt");
            var value = new DateTime(2026, 10, 1, 9, 0, 0, kind);

            var converted = (DateTime)converter.ConvertToProvider(value)!;

            var expected = kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
            Assert.Equal(expected.Ticks, converted.Ticks);
            Assert.Equal(expected.Kind, converted.Kind);
        }

        // I-057
        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void 値変換DateTime_読み出し_値はそのままでKindがUtc(DateTimeKind kind)
        {
            var converter = Converter(typeof(Reservation), "RequestedPickupAt");
            var value = new DateTime(2026, 10, 1, 9, 0, 0, kind);

            var converted = (DateTime)converter.ConvertFromProvider(value)!;

            Assert.Equal(value.Ticks, converted.Ticks);
            Assert.Equal(DateTimeKind.Utc, converted.Kind);
        }

        // I-058
        [Fact]
        public void 値変換NullableDateTime_nullと値_nullはnullのまま値はDateTimeと同じ()
        {
            var converter = (ValueConverter<DateTime?, DateTime?>)Converter(typeof(Reservation), "CancelledAt");
            var toProvider = converter.ConvertToProviderExpression.Compile();
            var fromProvider = converter.ConvertFromProviderExpression.Compile();

            Assert.Null(toProvider(null));
            Assert.Null(fromProvider(null));

            var local = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Local);
            var utc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
            var unspecified = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(local.ToUniversalTime(), toProvider(local));
            Assert.Equal(DateTimeKind.Utc, toProvider(local)!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, toProvider(utc)!.Value.Kind);
            Assert.Equal(DateTimeKind.Unspecified, toProvider(unspecified)!.Value.Kind);
            Assert.Equal(unspecified.Ticks, toProvider(unspecified)!.Value.Ticks);

            var read = fromProvider(unspecified)!.Value;
            Assert.Equal(unspecified.Ticks, read.Ticks);
            Assert.Equal(DateTimeKind.Utc, read.Kind);
        }
    }
}
