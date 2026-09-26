using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Backend.Tests.TestDoubles;
using Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Tests.Infrastructure;

public class JwtAccessTokenIssuerTests
{
    private static JwtOptions Options(int expiresMinutes = 60)
    {
        var options = JwtOptionsTests.ValidOptions();
        options.ExpiresMinutes = expiresMinutes;
        return options;
    }

    private static JwtAccessTokenIssuer CreateIssuer(JwtOptions options) =>
        new(Microsoft.Extensions.Options.Options.Create(options));

    private static Task<TokenValidationResult> ValidateAsync(string token, JwtOptions options) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());

    // 条件を変えたトークンを作る
    private static string CreateToken(
        JwtOptions options,
        string? issuer = null,
        string? audience = null,
        byte[]? key = null,
        string algorithm = SecurityAlgorithms.HmacSha256,
        DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? options.Issuer,
            Audience = audience ?? options.Audience,
            IssuedAt = now.AddMinutes(-10),
            NotBefore = now.AddMinutes(-10),
            Expires = expires ?? now.AddMinutes(10),
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key ?? Encoding.UTF8.GetBytes(options.SigningKey)), algorithm)
        });
    }

    // I-014
    [Fact]
    public async Task Issue_正常な設定_自身の検証設定で検証できる()
    {
        var options = Options();
        var token = CreateIssuer(options).Issue(TestUsers.Rider());

        var result = await ValidateAsync(token.Token, options);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    // I-015
    [Fact]
    public void Issue_正常な設定_subがユーザーIDの文字列()
    {
        var user = TestUsers.Rider();
        var token = new JsonWebToken(CreateIssuer(Options()).Issue(user).Token);

        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.True(Guid.TryParse(token.Subject, out var parsed));
        Assert.Equal(user.Id, parsed);
    }

    // I-016
    [Fact]
    public void Issue_正常な設定_issとaudが設定値()
    {
        var options = Options();
        var token = new JsonWebToken(CreateIssuer(options).Issue(TestUsers.Rider()).Token);

        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Equal([options.Audience], token.Audiences);
    }

    // I-017
    [Fact]
    public void Issue_正常な設定_ヘッダーがHS256とJWT()
    {
        var token = new JsonWebToken(CreateIssuer(Options()).Issue(TestUsers.Rider()).Token);

        Assert.Equal("HS256", token.Alg);
        Assert.Equal("JWT", token.Typ);
    }

    // I-018
    [Fact]
    public void Issue_有効期限60分_ExpiresAtが呼び出し前後の60分後の間でUtc()
    {
        var issuer = CreateIssuer(Options(60));

        var before = DateTime.UtcNow;
        var token = issuer.Issue(TestUsers.Rider());
        var after = DateTime.UtcNow;

        Assert.InRange(token.ExpiresAt, before.AddMinutes(60), after.AddMinutes(60));
        Assert.Equal(DateTimeKind.Utc, token.ExpiresAt.Kind);
    }

    // I-019
    [Fact]
    public void Issue_有効期限5分_ExpiresAtが約5分後()
    {
        var issuer = CreateIssuer(Options(5));

        var before = DateTime.UtcNow;
        var token = issuer.Issue(TestUsers.Rider());
        var after = DateTime.UtcNow;

        Assert.InRange(token.ExpiresAt, before.AddMinutes(5), after.AddMinutes(5));
    }

    // I-020
    [Fact]
    public void Issue_有効期限5分_expとExpiresAtの差が1秒未満()
    {
        var issued = CreateIssuer(Options(5)).Issue(TestUsers.Rider());
        var token = new JsonWebToken(issued.Token);

        // exp は秒単位に切り捨てられる
        var diff = issued.ExpiresAt - token.ValidTo;
        Assert.InRange(diff, TimeSpan.Zero, TimeSpan.FromSeconds(1) - TimeSpan.FromTicks(1));
    }

    // I-021
    [Fact]
    public void Issue_正常な設定_個人情報をクレームに含めない()
    {
        var user = TestUsers.Rider();
        var raw = CreateIssuer(Options(5)).Issue(user).Token;
        var token = new JsonWebToken(raw);

        var claimTypes = token.Claims.Select(c => c.Type).Distinct().OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "aud", "exp", "iat", "iss", "nbf", "sub" }, claimTypes);

        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(raw.Split('.')[1]));
        Assert.DoesNotContain(user.Email, payload);
        Assert.DoesNotContain(user.LastName, payload);
        Assert.DoesNotContain("rider", payload, StringComparison.OrdinalIgnoreCase);
    }

    // I-022
    [Fact]
    public async Task 検証設定_ペイロードのsubを改ざん_検証失敗()
    {
        var options = Options();
        var parts = CreateIssuer(options).Issue(TestUsers.Rider()).Token.Split('.');

        var payload = JsonNode.Parse(Base64UrlEncoder.DecodeBytes(parts[1]))!.AsObject();
        payload["sub"] = Guid.NewGuid().ToString();
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload))}.{parts[2]}";

        var result = await ValidateAsync(tampered, options);

        Assert.False(result.IsValid);
    }

    // I-023
    [Fact]
    public async Task 検証設定_別の鍵で署名_検証失敗()
    {
        var options = Options();
        var token = CreateToken(options, key: Encoding.UTF8.GetBytes("ffffffffffffffffffffffffffffffff"));

        Assert.False((await ValidateAsync(token, options)).IsValid);
    }

    // I-024
    [Fact]
    public async Task 検証設定_発行者が違う_検証失敗()
    {
        var options = Options();
        var token = CreateToken(options, issuer: "Other");

        Assert.False((await ValidateAsync(token, options)).IsValid);
    }

    // I-025
    [Fact]
    public async Task 検証設定_対象者が違う_検証失敗()
    {
        var options = Options();
        var token = CreateToken(options, audience: "Other");

        Assert.False((await ValidateAsync(token, options)).IsValid);
    }

    // I-026
    [Fact]
    public async Task 検証設定_同じ鍵でHS512署名_検証失敗()
    {
        // HS512 の署名には 64 バイト以上の鍵が必要なので、64 バイトの SigningKey で比べる
        var options = Options();
        options.SigningKey = JwtOptionsTests.ValidSigningKey + JwtOptionsTests.ValidSigningKey;
        Assert.True((await ValidateAsync(CreateToken(options), options)).IsValid); // 同条件の HS256 は通る

        var token = CreateToken(options, algorithm: SecurityAlgorithms.HmacSha512);

        Assert.False((await ValidateAsync(token, options)).IsValid);
    }

    // I-027
    [Fact]
    public async Task 検証設定_algがnoneの未署名トークン_検証失敗()
    {
        var options = Options();
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["iat"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(10).ToUnixTimeSeconds()
        }));

        var result = await ValidateAsync($"{header}.{payload}.", options);

        Assert.False(result.IsValid);
    }

    // I-028
    [Theory]
    [InlineData(20, true)]
    [InlineData(40, false)]
    public async Task 検証設定_期限切れの秒数_30秒までは許容される(int expiredSecondsAgo, bool valid)
    {
        var options = Options();
        var token = CreateToken(options, expires: DateTime.UtcNow.AddSeconds(-expiredSecondsAgo));

        var result = await ValidateAsync(token, options);

        Assert.Equal(valid, result.IsValid);
    }

    // I-029
    [Fact]
    public void Issue_鍵が16バイトの不正な設定_例外になる()
    {
        var options = Options();
        options.SigningKey = new string('a', 16);

        // Validate を通していない設定では HS256 の鍵長不足で発行できない
        Assert.ThrowsAny<Exception>(() => CreateIssuer(options).Issue(TestUsers.Rider()));
    }
}
