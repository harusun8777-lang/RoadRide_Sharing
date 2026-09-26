using Backend.Tests.TestDoubles;
using AddUserRoleUseCase = Usecase.User.AddUserRoleUseCase;
using UserNotFoundException = Usecase.User.UserNotFoundException;
using UserRole = Domain.Users.UserRole;

namespace Backend.Tests.Usecase;

public class AddUserRoleUseCaseTests
{
    private readonly FakeUserRepository _users = new();
    private readonly AddUserRoleUseCase _useCase;

    public AddUserRoleUseCaseTests()
    {
        _useCase = new AddUserRoleUseCase(_users);
    }

    // U-034
    [Fact]
    public async Task ExecuteAsync_RiderにDriverを追加_両方を持ちRiderで稼働のまま保存される()
    {
        var user = TestUsers.Rider();
        _users.Seed(user);

        var result = await _useCase.ExecuteAsync(user.Id, UserRole.Driver);

        Assert.Equal(new[] { UserRole.Rider, UserRole.Driver }, result.Roles);
        Assert.Equal(UserRole.Rider, result.ActiveRole);
        Assert.Same(user, Assert.Single(_users.Updated));
    }

    // U-035
    [Fact]
    public async Task ExecuteAsync_DriverにRiderを追加_両方を持ちDriverで稼働のまま保存される()
    {
        var user = TestUsers.Driver();
        _users.Seed(user);

        var result = await _useCase.ExecuteAsync(user.Id, UserRole.Rider);

        Assert.Equal(new[] { UserRole.Rider, UserRole.Driver }, result.Roles);
        Assert.Equal(UserRole.Driver, result.ActiveRole);
        Assert.Single(_users.Updated);
    }

    // U-036
    [Fact]
    public async Task ExecuteAsync_未登録のId_UserNotFoundExceptionで保存されない()
    {
        var id = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _useCase.ExecuteAsync(id, UserRole.Driver));

        Assert.Equal(id, ex.UserId);
        Assert.Empty(_users.Updated);
    }

    // U-037
    [Fact]
    public async Task ExecuteAsync_登録済みの区分_InvalidOperationExceptionで保存されない()
    {
        var user = TestUsers.Rider();
        _users.Seed(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(user.Id, UserRole.Rider));

        Assert.Empty(_users.Updated);
    }

    // U-038
    [Fact]
    public async Task ExecuteAsync_定義外の区分_ArgumentOutOfRangeExceptionで保存されない()
    {
        var user = TestUsers.Rider();
        _users.Seed(user);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _useCase.ExecuteAsync(user.Id, (UserRole)99));

        Assert.Empty(_users.Updated);
    }
}
