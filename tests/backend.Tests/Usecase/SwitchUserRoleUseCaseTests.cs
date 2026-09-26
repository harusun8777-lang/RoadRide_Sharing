using Backend.Tests.TestDoubles;
using SwitchUserRoleUseCase = Usecase.User.SwitchUserRoleUseCase;
using UnfinishedActivityExistsException = Usecase.User.UnfinishedActivityExistsException;
using UserNotFoundException = Usecase.User.UserNotFoundException;
using UserRole = Domain.Users.UserRole;

namespace Backend.Tests.Usecase;

public class SwitchUserRoleUseCaseTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeReservationRepository _reservations = new();
    private readonly FakeRideGroupRepository _rideGroups = new();
    private readonly SwitchUserRoleUseCase _useCase;

    public SwitchUserRoleUseCaseTests()
    {
        _useCase = new SwitchUserRoleUseCase(_users, _reservations, _rideGroups);
    }

    // U-039
    [Fact]
    public async Task ExecuteAsync_RiderからDriver_未完了の予約なし_切り替わり保存される()
    {
        var user = TestUsers.Both(UserRole.Rider);
        _users.Seed(user);

        var result = await _useCase.ExecuteAsync(user.Id, UserRole.Driver);

        Assert.Equal(UserRole.Driver, result.ActiveRole);
        Assert.Single(_users.Updated);
        Assert.Equal(user.Id, Assert.Single(_reservations.HasUnfinishedCalls));
        Assert.Empty(_rideGroups.HasUnfinishedByDriverCalls);
    }

    // U-040
    [Fact]
    public async Task ExecuteAsync_DriverからRider_未完了の運行なし_切り替わり保存される()
    {
        var user = TestUsers.Both(UserRole.Driver);
        _users.Seed(user);

        var result = await _useCase.ExecuteAsync(user.Id, UserRole.Rider);

        Assert.Equal(UserRole.Rider, result.ActiveRole);
        Assert.Single(_users.Updated);
        Assert.Equal(user.Id, Assert.Single(_rideGroups.HasUnfinishedByDriverCalls));
        Assert.Empty(_reservations.HasUnfinishedCalls);
    }

    // U-041
    [Fact]
    public async Task ExecuteAsync_未完了の予約あり_UnfinishedActivityExistsExceptionで保存されない()
    {
        var user = TestUsers.Both(UserRole.Rider);
        _users.Seed(user);
        _reservations.UsersWithUnfinished.Add(user.Id);

        var ex = await Assert.ThrowsAsync<UnfinishedActivityExistsException>(
            () => _useCase.ExecuteAsync(user.Id, UserRole.Driver));

        Assert.Equal(user.Id, ex.UserId);
        Assert.Equal(UserRole.Rider, ex.ActiveRole);
        Assert.Equal(UserRole.Rider, user.ActiveRole);
        Assert.Empty(_users.Updated);
    }

    // U-042
    [Fact]
    public async Task ExecuteAsync_未完了の運行あり_UnfinishedActivityExistsExceptionで保存されない()
    {
        var user = TestUsers.Both(UserRole.Driver);
        _users.Seed(user);
        _rideGroups.DriversWithUnfinished.Add(user.Id);

        var ex = await Assert.ThrowsAsync<UnfinishedActivityExistsException>(
            () => _useCase.ExecuteAsync(user.Id, UserRole.Rider));

        Assert.Equal(UserRole.Driver, ex.ActiveRole);
        Assert.Empty(_users.Updated);
    }

    // U-043
    [Fact]
    public async Task ExecuteAsync_同じ区分を指定_未完了チェックせず成功し保存される()
    {
        var user = TestUsers.Both(UserRole.Rider);
        _users.Seed(user);
        _reservations.UsersWithUnfinished.Add(user.Id);

        var result = await _useCase.ExecuteAsync(user.Id, UserRole.Rider);

        Assert.Equal(UserRole.Rider, result.ActiveRole);
        Assert.Empty(_reservations.HasUnfinishedCalls);
        Assert.Empty(_rideGroups.HasUnfinishedByDriverCalls);
        Assert.Single(_users.Updated);
    }

    // U-044
    [Fact]
    public async Task ExecuteAsync_切り替え先の区分が未登録_InvalidOperationExceptionで保存されない()
    {
        var user = TestUsers.Rider();
        _users.Seed(user);

        // ThrowsAsync は型の完全一致なので、派生の UnfinishedActivityExistsException は通らない
        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(user.Id, UserRole.Driver));

        Assert.Empty(_users.Updated);
    }

    // U-045
    [Fact]
    public async Task ExecuteAsync_未登録かつ未完了あり_未完了のエラーが先()
    {
        var user = TestUsers.Rider();
        _users.Seed(user);
        _reservations.UsersWithUnfinished.Add(user.Id);

        await Assert.ThrowsAsync<UnfinishedActivityExistsException>(
            () => _useCase.ExecuteAsync(user.Id, UserRole.Driver));

        Assert.Empty(_users.Updated);
    }

    // U-046
    [Fact]
    public async Task ExecuteAsync_未登録のId_UserNotFoundExceptionで何も呼ばない()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(() => _useCase.ExecuteAsync(Guid.NewGuid(), UserRole.Driver));

        Assert.Empty(_reservations.HasUnfinishedCalls);
        Assert.Empty(_rideGroups.HasUnfinishedByDriverCalls);
        Assert.Empty(_users.Updated);
    }
}
