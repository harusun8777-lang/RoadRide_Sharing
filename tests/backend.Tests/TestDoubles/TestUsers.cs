using Domain.Users;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.TestDoubles
{
    // テスト用のユーザーを作る。パスワードハッシュの既定は FakePasswordHasher の形式
    public static class TestUsers
    {
        public const string Password = "password1234";
        public const string DefaultPasswordHash = FakePasswordHasher.Prefix + Password;

        public static DomainUser Rider(string? email = null, string? passwordHash = null)
            => Create(UserRole.Rider, email, passwordHash);

        public static DomainUser Driver(string? email = null, string? passwordHash = null)
            => Create(UserRole.Driver, email, passwordHash);

        // Rider と Driver の両方を持ち、active で稼働している
        public static DomainUser Both(UserRole active, string? email = null, string? passwordHash = null)
        {
            var other = active == UserRole.Rider ? UserRole.Driver : UserRole.Rider;
            var user = Create(other, email, passwordHash);
            user.AddRole(active);
            user.SwitchRole(active);
            return user;
        }

        private static DomainUser Create(UserRole role, string? email, string? passwordHash)
            => DomainUser.Create(
                email ?? $"user-{Guid.NewGuid():N}@example.com",
                passwordHash ?? DefaultPasswordHash,
                "山田",
                "太郎",
                "ヤマダ",
                "タロウ",
                role);
    }
}
