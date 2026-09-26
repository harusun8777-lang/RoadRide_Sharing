using Backend.Tests.TestDoubles;
using GetCurrentUserUseCase = Usecase.User.GetCurrentUserUseCase;

namespace Backend.Tests.Usecase;

public class GetCurrentUserUseCaseTests
{
    private readonly FakeUserRepository _users = new();
    private readonly GetCurrentUserUseCase _useCase;

    public GetCurrentUserUseCaseTests()
    {
        _useCase = new GetCurrentUserUseCase(_users);
    }

    // U-030
    [Fact]
    public async Task ExecuteAsync_登録済みユーザーのId_そのユーザーが返る()
    {
        var user = TestUsers.Rider();
        _users.Seed(user);

        var result = await _useCase.ExecuteAsync(user.Id.ToString());

        Assert.Same(user, result);
        Assert.Equal(user.Id, Assert.Single(_users.FindByIdCalls));
    }

    // U-031
    [Fact]
    public async Task ExecuteAsync_未登録のId_nullが返る()
    {
        var result = await _useCase.ExecuteAsync(Guid.NewGuid().ToString());

        Assert.Null(result);
    }

    // U-032
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public async Task ExecuteAsync_不正なsubject_nullが返りリポジトリを呼ばない(string? subject)
    {
        var result = await _useCase.ExecuteAsync(subject);

        Assert.Null(result);
        Assert.Empty(_users.FindByIdCalls);
    }

    // U-033
    [Theory]
    [InlineData("N")]
    [InlineData("B")]
    public async Task ExecuteAsync_UUIDの表記ゆれ_そのユーザーが返る(string format)
    {
        var user = TestUsers.Rider();
        _users.Seed(user);

        var result = await _useCase.ExecuteAsync(user.Id.ToString(format));

        Assert.Same(user, result);
    }
}
