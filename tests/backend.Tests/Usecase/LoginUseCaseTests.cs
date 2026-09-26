using Backend.Tests.TestDoubles;
using Usecase.Auth;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Usecase;

public class LoginUseCaseTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAccessTokenIssuer _issuer = new();
    private readonly LoginUseCase _useCase;

    public LoginUseCaseTests()
    {
        _useCase = new LoginUseCase(_users, _hasher, _issuer);
    }

    private DomainUser SeedRider(string email = "taro@example.com")
    {
        var user = TestUsers.Rider(email);
        _users.Seed(user);
        return user;
    }

    // U-001
    [Fact]
    public async Task ExecuteAsync_正しい資格情報_発行されたトークンが返る()
    {
        var user = SeedRider();

        var token = await _useCase.ExecuteAsync("taro@example.com", TestUsers.Password);

        Assert.Equal($"token-{user.Id}", token.Token);
        Assert.Equal(FakeAccessTokenIssuer.ExpiresAt, token.ExpiresAt);
        Assert.Same(user, Assert.Single(_issuer.IssuedFor));
    }

    // U-002
    [Fact]
    public async Task ExecuteAsync_メールアドレスの前後に空白_トリムして検索し成功する()
    {
        SeedRider();

        await _useCase.ExecuteAsync("  taro@example.com ", TestUsers.Password);

        Assert.Equal("taro@example.com", Assert.Single(_users.FindByEmailCalls));
        Assert.Single(_issuer.IssuedFor);
    }

    // U-003
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_空のメールアドレス_InvalidCredentialsExceptionで検索しない(string? email)
    {
        SeedRider();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync(email!, TestUsers.Password));

        Assert.Empty(_users.FindByEmailCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    // U-004
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ExecuteAsync_空のパスワード_InvalidCredentialsExceptionで検索しない(string? password)
    {
        SeedRider();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("taro@example.com", password!));

        Assert.Empty(_users.FindByEmailCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    // U-005
    [Fact]
    public async Task ExecuteAsync_空白のみのパスワード_Verifyまで進みInvalidCredentialsException()
    {
        SeedRider();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("taro@example.com", "   "));

        Assert.Equal("   ", Assert.Single(_hasher.VerifyCalls).Password);
        Assert.Empty(_issuer.IssuedFor);
    }

    // U-006
    [Fact]
    public async Task ExecuteAsync_未登録のメールアドレス_InvalidCredentialsExceptionでVerifyしない()
    {
        SeedRider();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("unknown@example.com", TestUsers.Password));

        Assert.Empty(_hasher.VerifyCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    // U-007
    [Fact]
    public async Task ExecuteAsync_パスワード違い_InvalidCredentialsException()
    {
        SeedRider();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("taro@example.com", "wrong-password"));

        Assert.Empty(_issuer.IssuedFor);
    }

    // U-008
    [Fact]
    public async Task ExecuteAsync_正しい資格情報_Verifyにハッシュとパスワードの順で渡す()
    {
        var user = SeedRider();

        await _useCase.ExecuteAsync("taro@example.com", TestUsers.Password);

        var call = Assert.Single(_hasher.VerifyCalls);
        Assert.Equal(user.PasswordHash, call.PasswordHash);
        Assert.Equal(TestUsers.Password, call.Password);
    }

    // U-009
    [Fact]
    public async Task ExecuteAsync_パスワードの前後に空白_トリムせずVerifyに渡す()
    {
        SeedRider();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("taro@example.com", " password1234 "));

        Assert.Equal(" password1234 ", Assert.Single(_hasher.VerifyCalls).Password);
    }

    // U-010
    [Fact]
    public async Task ExecuteAsync_未登録とパスワード違い_例外の型とメッセージが同じ()
    {
        SeedRider();

        var notFound = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("unknown@example.com", TestUsers.Password));
        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _useCase.ExecuteAsync("taro@example.com", "wrong-password"));

        Assert.Equal(notFound.GetType(), wrongPassword.GetType());
        Assert.Equal("Email or password is incorrect.", notFound.Message);
        Assert.Equal(notFound.Message, wrongPassword.Message);
    }

    // U-011
    [Fact]
    public async Task ExecuteAsync_Driverで稼働中のユーザー_トークンが返る()
    {
        var user = TestUsers.Driver("driver@example.com");
        _users.Seed(user);

        var token = await _useCase.ExecuteAsync("driver@example.com", TestUsers.Password);

        Assert.Equal($"token-{user.Id}", token.Token);
    }
}
