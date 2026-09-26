using Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backend.Tests.Infrastructure;

public class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    // I-030
    [Fact]
    public void Hash_パスワード_空でなく平文を含まない()
    {
        var hash = _hasher.Hash("password123");

        Assert.False(string.IsNullOrEmpty(hash));
        Assert.DoesNotContain("password123", hash);
    }

    // I-031
    [Fact]
    public void Hash_同じパスワードを2回_ハッシュが異なる()
    {
        Assert.NotEqual(_hasher.Hash("password123"), _hasher.Hash("password123"));
    }

    // I-032
    [Fact]
    public void Hash_長さ128のパスワード_255文字以下()
    {
        var hash = _hasher.Hash(new string('p', 128));

        Assert.InRange(hash.Length, 1, 255);
    }

    // I-033
    [Fact]
    public void Hash_パスワード_IdentityV3形式()
    {
        var bytes = Convert.FromBase64String(_hasher.Hash("password123"));

        Assert.Equal(0x01, bytes[0]);
    }

    // I-034
    [Fact]
    public void Verify_正しいパスワード_true()
    {
        Assert.True(_hasher.Verify(_hasher.Hash("password123"), "password123"));
    }

    // I-035
    [Fact]
    public void Verify_違うパスワード_false()
    {
        Assert.False(_hasher.Verify(_hasher.Hash("password123"), "password124"));
    }

    // I-036
    [Fact]
    public void Verify_大文字小文字が違う_false()
    {
        Assert.False(_hasher.Verify(_hasher.Hash("Password123"), "password123"));
    }

    // I-037
    [Fact]
    public void Verify_非ASCIIのパスワード_true()
    {
        const string password = "パスワード🔑abc";

        Assert.True(_hasher.Verify(_hasher.Hash(password), password));
    }

    // I-038
    [Fact]
    public void Verify_別インスタンスで作ったハッシュ_true()
    {
        var hash = new AspNetPasswordHasher().Hash("password123");

        Assert.True(new AspNetPasswordHasher().Verify(hash, "password123"));
    }

    // I-039
    [Fact]
    public void Verify_空のハッシュ_false()
    {
        Assert.False(_hasher.Verify("", "password123"));
    }

    // I-040
    [Fact]
    public void Verify_IdentityV2形式のハッシュ_true()
    {
        var v2 = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));
        var hash = v2.HashPassword(new object(), "password123");

        // SuccessRehashNeeded も成功として扱う
        Assert.True(_hasher.Verify(hash, "password123"));
    }

    // I-041
    [Fact]
    public void Verify_Base64でないハッシュ_FormatExceptionになる_現状の挙動()
    {
        // false ではなく例外になる。DB のハッシュが壊れているとログインが 500 になる（設計書 5 章 #15）
        Assert.Throws<FormatException>(() => _hasher.Verify("not-a-hash", "password123"));
    }
}
