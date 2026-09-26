using System.Text;
using Infrastructure.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Tests.Infrastructure;

public class JwtOptionsTests
{
    public const string ValidSigningKey = "0123456789abcdef0123456789abcdef"; // ASCII 32 文字

    // 正常な設定
    public static JwtOptions ValidOptions() => new()
    {
        Issuer = "RoadRideSharing",
        Audience = "RoadRideSharing",
        SigningKey = ValidSigningKey,
        ExpiresMinutes = 60
    };

    // I-001
    [Fact]
    public void Validate_正常な設定_例外にならない()
    {
        var ex = Record.Exception(() => ValidOptions().Validate());
        Assert.Null(ex);
    }

    // I-002
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Issuerが未設定_InvalidOperationException(string issuer)
    {
        var options = ValidOptions();
        options.Issuer = issuer;

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("Jwt:Issuer", ex.Message);
    }

    // I-003
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Audienceが未設定_InvalidOperationException(string audience)
    {
        var options = ValidOptions();
        options.Audience = audience;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    // I-004
    [Fact]
    public void Validate_鍵がASCII31文字_InvalidOperationException()
    {
        var options = ValidOptions();
        options.SigningKey = new string('a', 31);

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("32 bytes", ex.Message);
    }

    // I-005
    [Fact]
    public void Validate_鍵がASCII32文字_例外にならない()
    {
        var options = ValidOptions();
        options.SigningKey = new string('a', 32);

        Assert.Null(Record.Exception(options.Validate));
    }

    // I-006
    [Theory]
    [InlineData(10, false)] // 30 バイト
    [InlineData(11, true)]  // 33 バイト
    public void Validate_鍵の長さはUTF8のバイト数で判定_32バイト以上だけ通る(int count, bool valid)
    {
        var options = ValidOptions();
        options.SigningKey = new string('あ', count);

        var ex = Record.Exception(options.Validate);

        if (valid)
            Assert.Null(ex);
        else
            Assert.IsType<InvalidOperationException>(ex);
    }

    // I-007
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_有効期限が0以下_InvalidOperationException(int minutes)
    {
        var options = ValidOptions();
        options.ExpiresMinutes = minutes;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    // I-008
    [Fact]
    public void Validate_有効期限が1分_例外にならない()
    {
        var options = ValidOptions();
        options.ExpiresMinutes = 1;

        Assert.Null(Record.Exception(options.Validate));
    }

    // I-009
    [Fact]
    public void 既定値_newしただけ_空の設定で検証に失敗する()
    {
        var options = new JwtOptions();

        Assert.Equal("Jwt", JwtOptions.SectionName);
        Assert.Equal(60, options.ExpiresMinutes);
        Assert.Equal("", options.Issuer);
        Assert.Equal("", options.Audience);
        Assert.Equal("", options.SigningKey);
        // 設定漏れのまま起動しないこと
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    // I-010
    [Fact]
    public void CreateSigningKey_正常な設定_鍵がSigningKeyのUTF8バイト列()
    {
        var key = ValidOptions().CreateSigningKey();

        Assert.Equal(Encoding.UTF8.GetBytes(ValidSigningKey), key.Key);
    }

    // I-011
    [Fact]
    public void CreateValidationParameters_正常な設定_設定値とHS256のみと30秒の許容()
    {
        var options = ValidOptions();

        var parameters = options.CreateValidationParameters();

        Assert.Equal(options.Issuer, parameters.ValidIssuer);
        Assert.Equal(options.Audience, parameters.ValidAudience);
        Assert.Equal([SecurityAlgorithms.HmacSha256], parameters.ValidAlgorithms);
        Assert.Equal(TimeSpan.FromSeconds(30), parameters.ClockSkew);
        var key = Assert.IsType<SymmetricSecurityKey>(parameters.IssuerSigningKey);
        Assert.Equal(options.CreateSigningKey().Key, key.Key);
    }

    // I-012
    [Fact]
    public void CreateValidationParameters_正常な設定_検証が無効化されていない()
    {
        var parameters = ValidOptions().CreateValidationParameters();

        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.RequireSignedTokens);
        Assert.True(parameters.RequireExpirationTime);
    }

    // I-013
    [Fact]
    public void Validate_空白だけの鍵_例外にならない_現状の挙動()
    {
        var options = ValidOptions();
        options.SigningKey = new string(' ', 32);

        // バイト数しか見ないので通ってしまう（設計書 5 章 #13）
        Assert.Null(Record.Exception(options.Validate));
    }
}
