using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Usecase.Auth;
using DomainUser = Domain.Users.User;
using IPasswordHasher = Usecase.User.IPasswordHasher;

namespace Infrastructure.Auth
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";
        // HS256 の鍵は 256bit 以上が必要
        public const int MinSigningKeyBytes = 32;

        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SigningKey { get; set; } = string.Empty;
        public int ExpiresMinutes { get; set; } = 60;

        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

        public TokenValidationParameters CreateValidationParameters() => new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = CreateSigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
                throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");
            if (Encoding.UTF8.GetByteCount(SigningKey) < MinSigningKeyBytes)
                throw new InvalidOperationException($"Jwt:SigningKey must be at least {MinSigningKeyBytes} bytes.");
            if (ExpiresMinutes <= 0)
                throw new InvalidOperationException("Jwt:ExpiresMinutes must be positive.");
        }
    }

    public class JwtAccessTokenIssuer : IAccessTokenIssuer
    {
        private readonly JwtOptions _options;
        private readonly JsonWebTokenHandler _handler = new();

        public JwtAccessTokenIssuer(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public AccessToken Issue(DomainUser user)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
            var token = _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Expires = expiresAt,
                Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())]),
                SigningCredentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
            });
            return new AccessToken(token, expiresAt);
        }
    }

    // ASP.NET Core Identity の PasswordHasher（PBKDF2）を使う
    public class AspNetPasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<DomainUser> _hasher = new();

        public string Hash(string password) => _hasher.HashPassword(null!, password);

        public bool Verify(string passwordHash, string password) =>
            _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
    }
}
