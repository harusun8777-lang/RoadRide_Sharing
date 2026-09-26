using Backend.Tests.TestDoubles;
using EmailAlreadyRegisteredException = Usecase.User.EmailAlreadyRegisteredException;
using RegisterUserUseCase = Usecase.User.RegisterUserUseCase;
using DomainUser = Domain.Users.User;
using UserRole = Domain.Users.UserRole;

namespace Backend.Tests.Usecase;

public class RegisterUserUseCaseTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly RegisterUserUseCase _useCase;

    public RegisterUserUseCaseTests()
    {
        _useCase = new RegisterUserUseCase(_users, _hasher);
    }

    // 既定値から指定した項目だけ変えて実行する
    private Task<DomainUser> Execute(
        string? email = "taro@example.com",
        string? password = "password1234",
        string? lastName = "山田",
        string? firstName = "太郎",
        string? kanaLastName = "ヤマダ",
        string? kanaFirstName = "タロウ",
        UserRole role = UserRole.Rider)
        => _useCase.ExecuteAsync(email!, password!, lastName!, firstName!, kanaLastName!, kanaFirstName!, role);

    // U-012
    [Fact]
    public async Task ExecuteAsync_Riderで登録_Riderで稼働するユーザーが保存される()
    {
        var user = await Execute();

        Assert.Equal(UserRole.Rider, user.ActiveRole);
        Assert.Equal(new[] { UserRole.Rider }, user.Roles);
        Assert.NotNull(user.Rider);
        Assert.Same(user, Assert.Single(_users.Added));
    }

    // U-013
    [Fact]
    public async Task ExecuteAsync_Driverで登録_Driverで稼働するユーザーが保存される()
    {
        var user = await Execute(role: UserRole.Driver);

        Assert.Equal(UserRole.Driver, user.ActiveRole);
        Assert.Equal(new[] { UserRole.Driver }, user.Roles);
        Assert.Single(_users.Added);
    }

    // U-014
    [Fact]
    public async Task ExecuteAsync_既定値_ハッシュ化したパスワードを保存する()
    {
        var user = await Execute();

        Assert.Equal("hashed:password1234", user.PasswordHash);
        Assert.NotEqual("password1234", user.PasswordHash);
        Assert.Equal("password1234", Assert.Single(_hasher.HashCalls));
    }

    // U-015, U-016
    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public async Task ExecuteAsync_パスワードが境界の長さ_登録される(int length)
    {
        await Execute(password: new string('a', length));

        Assert.Single(_users.Added);
    }

    // U-017, U-018
    [Theory]
    [InlineData(7)]
    [InlineData(129)]
    public async Task ExecuteAsync_パスワードが範囲外の長さ_ArgumentExceptionで何も呼ばない(int length)
    {
        await AssertPasswordRejected(new string('a', length));
    }

    // U-019
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ExecuteAsync_パスワードなし_ArgumentExceptionで何も呼ばない(string? password)
    {
        await AssertPasswordRejected(password);
    }

    private async Task AssertPasswordRejected(string? password)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Execute(password: password));

        Assert.Equal("password", ex.ParamName);
        Assert.Empty(_users.FindByEmailCalls);
        Assert.Empty(_hasher.HashCalls);
        Assert.Empty(_users.Added);
    }

    // U-020
    [Fact]
    public async Task ExecuteAsync_メールアドレス重複_EmailAlreadyRegisteredExceptionで保存されない()
    {
        _users.Seed(TestUsers.Rider("taro@example.com"));

        var ex = await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => Execute());

        Assert.Equal("taro@example.com", ex.Email);
        Assert.Empty(_hasher.HashCalls);
        Assert.Empty(_users.Added);
    }

    // U-021
    [Fact]
    public async Task ExecuteAsync_前後に空白のある重複メールアドレス_トリムして判定する()
    {
        _users.Seed(TestUsers.Rider("taro@example.com"));

        var ex = await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => Execute(email: " taro@example.com "));

        Assert.Equal("taro@example.com", ex.Email);
        Assert.Equal("taro@example.com", Assert.Single(_users.FindByEmailCalls));
    }

    // U-022
    [Fact]
    public async Task ExecuteAsync_短いパスワードかつ重複メールアドレス_パスワードのエラーが先()
    {
        _users.Seed(TestUsers.Rider("taro@example.com"));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Execute(password: "1234567"));

        Assert.Equal("password", ex.ParamName);
        Assert.Empty(_users.FindByEmailCalls);
    }

    // U-023
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_空のメールアドレス_重複チェックを飛ばしドメインでArgumentException(string? email)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Execute(email: email));

        Assert.Equal("email", ex.ParamName);
        Assert.Empty(_users.FindByEmailCalls);
        Assert.Empty(_users.Added);
    }

    // U-024
    [Theory]
    [InlineData("lastName", "")]
    [InlineData("lastName", "   ")]
    [InlineData("firstName", "")]
    [InlineData("firstName", "   ")]
    public async Task ExecuteAsync_姓名が空_ArgumentExceptionで保存されない(string field, string value)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => field == "lastName"
            ? Execute(lastName: value)
            : Execute(firstName: value));

        Assert.Equal(field, ex.ParamName);
        Assert.Empty(_users.Added);
    }

    // U-025
    [Theory]
    [InlineData("kanaLastName", "やまだ")]
    [InlineData("kanaLastName", "ﾔﾏﾀﾞ")]
    [InlineData("kanaLastName", "")]
    [InlineData("kanaFirstName", "たろう")]
    [InlineData("kanaFirstName", "ﾀﾛｳ")]
    [InlineData("kanaFirstName", "")]
    public async Task ExecuteAsync_読み仮名が不正_ArgumentExceptionで保存されない(string field, string value)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => field == "kanaLastName"
            ? Execute(kanaLastName: value)
            : Execute(kanaFirstName: value));

        Assert.Equal(field, ex.ParamName);
        Assert.Empty(_users.Added);
    }

    // U-026
    [Fact]
    public async Task ExecuteAsync_メールアドレスと姓名の前後に空白_トリムして保存する()
    {
        var user = await Execute(email: " taro@example.com ", lastName: " 山田 ", firstName: "　太郎 ");

        Assert.Equal("taro@example.com", user.Email);
        Assert.Equal("山田", user.LastName);
        Assert.Equal("太郎", user.FirstName);
    }

    // U-027
    [Fact]
    public async Task ExecuteAsync_空白8文字のパスワード_登録される()
    {
        await Execute(password: new string(' ', 8));

        Assert.Single(_users.Added);
    }

    // U-028
    [Fact]
    public async Task ExecuteAsync_絵文字4文字のパスワード_UTF16で8単位として登録される()
    {
        var password = "😀😀😀😀";
        Assert.Equal(8, password.Length);

        await Execute(password: password);

        Assert.Single(_users.Added);
    }

    // U-029
    [Fact]
    public async Task ExecuteAsync_定義外の区分_ArgumentOutOfRangeExceptionで保存されない()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Execute(role: (UserRole)99));

        Assert.Empty(_users.Added);
    }
}
