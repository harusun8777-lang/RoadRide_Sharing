using global::Domain.Users;
using DomainUser = global::Domain.Users.User;

namespace Backend.Tests.Domain
{
    public class UserTests
    {
        private const string Email = "taro.yamada@example.com";
        private const string PasswordHash = "hash";
        private const string LastName = "山田";
        private const string FirstName = "太郎";
        private const string KanaLastName = "ヤマダ";
        private const string KanaFirstName = "タロウ";

        private const UserRole UndefinedRole = (UserRole)99;

        private static DomainUser CreateUser(
            UserRole initialRole = UserRole.Rider,
            string? email = Email,
            string? passwordHash = PasswordHash,
            string? lastName = LastName,
            string? firstName = FirstName,
            string? kanaLastName = KanaLastName,
            string? kanaFirstName = KanaFirstName)
            => DomainUser.Create(email!, passwordHash!, lastName!, firstName!, kanaLastName!, kanaFirstName!, initialRole);

        // 両ロールを保有し、active で稼働しているユーザー
        private static DomainUser CreateBoth(UserRole active)
        {
            var other = active == UserRole.Rider ? UserRole.Driver : UserRole.Rider;
            var user = CreateUser(other);
            user.AddRole(active);
            user.SwitchRole(active);
            return user;
        }

        // ---- Create ----

        // D-001
        [Fact]
        public void Create_既定値でRider_入力どおりのプロパティでRiderプロフィールのみ持つ()
        {
            var user = CreateUser(UserRole.Rider);

            Assert.NotEqual(Guid.Empty, user.Id);
            Assert.Equal(Email, user.Email);
            Assert.Equal(PasswordHash, user.PasswordHash);
            Assert.Equal(LastName, user.LastName);
            Assert.Equal(FirstName, user.FirstName);
            Assert.Equal(KanaLastName, user.KanaLastName);
            Assert.Equal(KanaFirstName, user.KanaFirstName);
            Assert.Equal(UserRole.Rider, user.ActiveRole);
            Assert.NotNull(user.Rider);
            Assert.Null(user.Driver);
        }

        // D-002
        [Fact]
        public void Create_既定値でDriver_Driverプロフィールのみ持つ()
        {
            var user = CreateUser(UserRole.Driver);

            Assert.Equal(UserRole.Driver, user.ActiveRole);
            Assert.NotNull(user.Driver);
            Assert.Null(user.Rider);
        }

        // D-003
        [Fact]
        public void Create_既定値_CreatedAtが呼び出し前後のUtc()
        {
            var before = DateTime.UtcNow;
            var user = CreateUser();
            var after = DateTime.UtcNow;

            Assert.InRange(user.CreatedAt, before, after);
            Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
        }

        // D-004
        [Fact]
        public void Create_2回生成_Idが異なる()
        {
            Assert.NotEqual(CreateUser().Id, CreateUser().Id);
        }

        // D-005
        [Fact]
        public void Create_前後に空白_Trimされて保持される()
        {
            var user = CreateUser(
                email: " a@example.com ",
                passwordHash: " hash ",
                lastName: " 山田 ",
                firstName: " 太郎 ");

            Assert.Equal("a@example.com", user.Email);
            Assert.Equal("hash", user.PasswordHash);
            Assert.Equal("山田", user.LastName);
            Assert.Equal("太郎", user.FirstName);
        }

        // D-006
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        public void Create_emailが空_ArgumentException(string? email)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(email: email));
            Assert.Equal("email", ex.ParamName);
        }

        // D-007
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_passwordHashが空_ArgumentException(string? passwordHash)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(passwordHash: passwordHash));
            Assert.Equal("passwordHash", ex.ParamName);
        }

        // D-008
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_lastNameが空_ArgumentException(string? lastName)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(lastName: lastName));
            Assert.Equal("lastName", ex.ParamName);
        }

        // D-009
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_firstNameが空_ArgumentException(string? firstName)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(firstName: firstName));
            Assert.Equal("firstName", ex.ParamName);
        }

        // D-010
        [Fact]
        public void Create_emailとlastNameが両方空_ParamNameはemail()
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(email: "", lastName: ""));
            Assert.Equal("email", ex.ParamName);
        }

        // D-011
        [Theory]
        [InlineData("ヤマダ")]
        [InlineData("ヴ")]
        [InlineData("ー")]
        [InlineData("ァ")] // ァ（範囲の先頭）
        [InlineData("ヶ")] // ヶ（範囲の末尾）
        [InlineData("ヤマダー")]
        public void Create_カナが許可範囲_そのまま保持される(string kanaLastName)
        {
            var user = CreateUser(kanaLastName: kanaLastName);
            Assert.Equal(kanaLastName, user.KanaLastName);
        }

        // D-012
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("やまだ")]
        [InlineData("ﾔﾏﾀﾞ")]
        [InlineData("山田")]
        [InlineData("Yamada")]
        [InlineData("ヤマ ダ")]
        [InlineData("ヤマ　ダ")] // 全角空白
        [InlineData("ヤマ・ダ")] // 中黒
        [InlineData("ヷ")] // ヷ（範囲外）
        [InlineData(" ヤマダ")]
        public void Create_kanaLastNameが不正_ArgumentException(string? kanaLastName)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(kanaLastName: kanaLastName));
            Assert.Equal("kanaLastName", ex.ParamName);
        }

        // D-013
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("たろう")]
        [InlineData("ﾀﾛｳ")]
        public void Create_kanaFirstNameが不正_ArgumentException(string? kanaFirstName)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateUser(kanaFirstName: kanaFirstName));
            Assert.Equal("kanaFirstName", ex.ParamName);
        }

        // D-014
        // 現状の挙動: .NET の $ は末尾の改行の直前にも一致するため、末尾の改行付きで受け付けてしまう（5. 既知の問題 1）
        [Fact]
        public void Create_カナの末尾に改行_現状は受け付けて改行ごと保持される()
        {
            var user = CreateUser(kanaLastName: "ヤマダ\n");
            Assert.Equal("ヤマダ\n", user.KanaLastName);
        }

        // D-015
        [Fact]
        public void Create_未定義のinitialRole_ArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateUser(UndefinedRole));
        }

        // D-016
        [Fact]
        public void Create_Rider_RiderプロフィールのUserIdとCreatedAtが設定される()
        {
            var before = DateTime.UtcNow;
            var user = CreateUser(UserRole.Rider);
            var after = DateTime.UtcNow;

            Assert.NotNull(user.Rider);
            Assert.Equal(user.Id, user.Rider.UserId);
            Assert.InRange(user.Rider.CreatedAt, before, after);
        }

        // D-017
        [Fact]
        public void Create_Driver_DriverプロフィールのUserIdとCreatedAtが設定される()
        {
            var before = DateTime.UtcNow;
            var user = CreateUser(UserRole.Driver);
            var after = DateTime.UtcNow;

            Assert.NotNull(user.Driver);
            Assert.Equal(user.Id, user.Driver.UserId);
            Assert.InRange(user.Driver.CreatedAt, before, after);
        }

        // ---- プロパティ ----

        // D-018
        [Fact]
        public void FullName_既定値_姓と名を半角空白で連結する()
        {
            Assert.Equal("山田 太郎", CreateUser().FullName);
        }

        // D-019
        [Fact]
        public void KanaFullName_既定値_姓と名を半角空白で連結する()
        {
            Assert.Equal("ヤマダ タロウ", CreateUser().KanaFullName);
        }

        // D-020
        [Fact]
        public void Roles_Riderのみ_Riderだけを返す()
        {
            Assert.Equal(new[] { UserRole.Rider }, CreateUser(UserRole.Rider).Roles);
        }

        // D-021
        [Fact]
        public void Roles_両ロール保有_enumの定義順で返す()
        {
            var user = CreateUser(UserRole.Rider);
            user.AddRole(UserRole.Driver);

            Assert.Equal(new[] { UserRole.Rider, UserRole.Driver }, user.Roles);
        }

        // ---- HasRole ----

        // D-022
        [Fact]
        public void HasRole_保有するロール_true()
        {
            Assert.True(CreateUser(UserRole.Rider).HasRole(UserRole.Rider));
        }

        // D-023
        [Fact]
        public void HasRole_保有しないロール_false()
        {
            Assert.False(CreateUser(UserRole.Rider).HasRole(UserRole.Driver));
        }

        // D-024
        [Fact]
        public void HasRole_未定義のロール_falseで例外にならない()
        {
            Assert.False(CreateUser().HasRole(UndefinedRole));
        }

        // ---- AddRole ----

        // D-025
        [Fact]
        public void AddRole_RiderにDriverを追加_Driverが作られActiveRoleは変わらない()
        {
            var user = CreateUser(UserRole.Rider);

            user.AddRole(UserRole.Driver);

            Assert.NotNull(user.Driver);
            Assert.Equal(user.Id, user.Driver.UserId);
            Assert.True(user.HasRole(UserRole.Driver));
            Assert.Equal(UserRole.Rider, user.ActiveRole);
        }

        // D-026
        [Fact]
        public void AddRole_DriverにRiderを追加_Riderが作られActiveRoleは変わらない()
        {
            var user = CreateUser(UserRole.Driver);

            user.AddRole(UserRole.Rider);

            Assert.NotNull(user.Rider);
            Assert.Equal(UserRole.Driver, user.ActiveRole);
        }

        // D-027
        [Fact]
        public void AddRole_保有済みのロール_InvalidOperationExceptionでプロフィールは作り直されない()
        {
            var user = CreateUser(UserRole.Rider);
            var rider = user.Rider;

            Assert.Throws<InvalidOperationException>(() => user.AddRole(UserRole.Rider));

            Assert.Same(rider, user.Rider);
        }

        // D-028
        [Fact]
        public void AddRole_同じロールを2回追加_2回目がInvalidOperationExceptionで1回目のインスタンスのまま()
        {
            var user = CreateUser(UserRole.Rider);
            user.AddRole(UserRole.Driver);
            var driver = user.Driver;

            Assert.Throws<InvalidOperationException>(() => user.AddRole(UserRole.Driver));

            Assert.Same(driver, user.Driver);
        }

        // D-029
        [Fact]
        public void AddRole_未定義のロール_ArgumentOutOfRangeException()
        {
            var user = CreateUser();
            Assert.Throws<ArgumentOutOfRangeException>(() => user.AddRole(UndefinedRole));
        }

        // ---- SwitchRole ----

        // D-030
        [Fact]
        public void SwitchRole_両ロール保有でRiderからDriver_ActiveRoleがDriverになる()
        {
            var user = CreateBoth(UserRole.Rider);

            user.SwitchRole(UserRole.Driver);

            Assert.Equal(UserRole.Driver, user.ActiveRole);
        }

        // D-031
        [Fact]
        public void SwitchRole_両ロール保有でDriverからRider_ActiveRoleがRiderになる()
        {
            var user = CreateBoth(UserRole.Driver);

            user.SwitchRole(UserRole.Rider);

            Assert.Equal(UserRole.Rider, user.ActiveRole);
        }

        // D-032
        [Fact]
        public void SwitchRole_現在と同じロール_例外なくActiveRoleはそのまま()
        {
            var user = CreateUser(UserRole.Rider);

            user.SwitchRole(UserRole.Rider);

            Assert.Equal(UserRole.Rider, user.ActiveRole);
        }

        // D-033
        [Theory]
        [InlineData(UserRole.Rider, UserRole.Driver)]
        [InlineData(UserRole.Driver, UserRole.Rider)]
        public void SwitchRole_保有しないロール_InvalidOperationExceptionでActiveRoleは変わらない(UserRole owned, UserRole target)
        {
            var user = CreateUser(owned);

            Assert.Throws<InvalidOperationException>(() => user.SwitchRole(target));

            Assert.Equal(owned, user.ActiveRole);
        }

        // D-034
        [Fact]
        public void SwitchRole_未定義のロール_InvalidOperationExceptionでActiveRoleは変わらない()
        {
            var user = CreateUser(UserRole.Rider);

            Assert.Throws<InvalidOperationException>(() => user.SwitchRole(UndefinedRole));

            Assert.Equal(UserRole.Rider, user.ActiveRole);
        }

        // ---- EnsureActiveAs ----

        // D-035
        [Fact]
        public void EnsureActiveAs_稼働中のロール_例外なし()
        {
            var user = CreateUser(UserRole.Rider);

            var ex = Record.Exception(() => user.EnsureActiveAs(UserRole.Rider));

            Assert.Null(ex);
        }

        // D-036
        [Fact]
        public void EnsureActiveAs_保有するが稼働していないロール_InvalidOperationException()
        {
            var user = CreateBoth(UserRole.Rider);

            Assert.Throws<InvalidOperationException>(() => user.EnsureActiveAs(UserRole.Driver));
        }

        // D-037
        [Fact]
        public void EnsureActiveAs_SwitchRole後_切り替え先のみ許可される()
        {
            var user = CreateBoth(UserRole.Rider);
            user.SwitchRole(UserRole.Driver);

            Assert.Null(Record.Exception(() => user.EnsureActiveAs(UserRole.Driver)));
            Assert.Throws<InvalidOperationException>(() => user.EnsureActiveAs(UserRole.Rider));
        }
    }
}
