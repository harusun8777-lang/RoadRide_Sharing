using System.Security.Claims;
using System.Text;
using Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Handler
{
    // テスト用の JWT を発行する。設定値は ApiFactory と共通
    public static class TestJwt
    {
        public const string Issuer = "RoadRideSharing.Tests";
        public const string Audience = "RoadRideSharing.Tests.Client";
        public const int ExpiresMinutes = 60;

        // HS512 のトークン（H-007）も作れるよう 64 バイト以上にする
        public const string SigningKey = "test-only-signing-key-for-handler-tests-0123456789-abcdefghijklmnopqrstuvwxyz";
        public const string OtherSigningKey = "another-signing-key-that-the-api-does-not-know-0123456789-abcdefghijklmnopqrst";

        public static JwtOptions CreateOptions() => new()
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = SigningKey,
            ExpiresMinutes = ExpiresMinutes
        };

        // 本体の JwtAccessTokenIssuer で正常なトークンを発行する
        public static string CreateToken(DomainUser user)
            => new JwtAccessTokenIssuer(Options.Create(CreateOptions())).Issue(user).Token;

        // 異常系のトークンを直接作る。subject が null なら sub を入れない
        public static string CreateCustomToken(
            string? subject,
            string signingKey = SigningKey,
            string algorithm = SecurityAlgorithms.HmacSha256,
            string issuer = Issuer,
            string audience = Audience,
            DateTime? expires = null)
        {
            var exp = expires ?? DateTime.UtcNow.AddMinutes(ExpiresMinutes);
            var claims = subject is null
                ? new List<Claim>()
                : new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };

            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                Expires = exp,
                // 過去の exp でも作れるよう nbf / iat も exp より前にする
                NotBefore = exp.AddHours(-1),
                IssuedAt = exp.AddHours(-1),
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), algorithm)
            });
        }
    }
}
