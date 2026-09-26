using Usecase.Auth;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.TestDoubles
{
    // 署名はせず、ユーザーIDを埋め込んだ文字列と固定の有効期限を返す
    public class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public static readonly DateTime ExpiresAt = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public List<DomainUser> IssuedFor { get; } = new();

        public AccessToken Issue(DomainUser user)
        {
            IssuedFor.Add(user);
            return new AccessToken($"token-{user.Id}", ExpiresAt);
        }
    }
}
