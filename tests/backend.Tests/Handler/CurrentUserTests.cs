using System.Security.Claims;
using CurrentUser = global::Handler.CurrentUser;

namespace Backend.Tests.Handler
{
    // HTTP を使わず ClaimsPrincipal を直接作って確認する
    public class CurrentUserTests
    {
        private static ClaimsPrincipal Principal(params Claim[] claims)
            => new(new ClaimsIdentity(claims, "Test"));

        // H-087
        [Fact]
        public void FindSubject_subあり_subの値を返す()
        {
            var principal = Principal(new Claim("sub", "0b8f7a9e-1111-2222-3333-444455556666"));

            Assert.Equal("0b8f7a9e-1111-2222-3333-444455556666", CurrentUser.FindSubject(principal));
        }

        // H-088
        [Fact]
        public void FindSubject_クレームなし_nullを返す()
        {
            Assert.Null(CurrentUser.FindSubject(Principal()));
        }

        // H-089
        [Fact]
        public void FindSubject_NameIdentifierだけ_nullを返す()
        {
            var principal = Principal(new Claim(ClaimTypes.NameIdentifier, "0b8f7a9e-1111-2222-3333-444455556666"));

            Assert.Null(CurrentUser.FindSubject(principal));
        }
    }
}
