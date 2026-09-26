using System.Globalization;
using System.Text.RegularExpressions;
using Backend.Tests.TestDoubles;
using RegisterReservationUseCase = Usecase.Reservation.RegisterReservationUseCase;
using UserNotFoundException = Usecase.User.UserNotFoundException;
using DomainReservation = Domain.Reservations.Reservation;
using DomainUser = Domain.Users.User;
using ReservationStatus = Domain.Reservations.ReservationStatus;
using UserRole = Domain.Users.UserRole;

namespace Backend.Tests.Usecase;

public class RegisterReservationUseCaseTests
{
    private static readonly DateTime DefaultPickupAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeReservationRepository _reservations = new();
    private readonly FakeUserRepository _users = new();
    private readonly RegisterReservationUseCase _useCase;

    public RegisterReservationUseCaseTests()
    {
        _useCase = new RegisterReservationUseCase(_reservations, _users);
    }

    private DomainUser SeedUser(DomainUser? user = null)
    {
        user ??= TestUsers.Rider();
        _users.Seed(user);
        return user;
    }

    // 既定値から指定した項目だけ変えて実行する
    private Task<DomainReservation> Execute(
        Guid userId,
        string pickupLocation = "市役所前",
        string destination = "中央病院",
        DateTime? requestedPickupAt = null,
        int passengerCount = 2,
        string? considerationNotes = "車椅子を使用")
        => _useCase.ExecuteAsync(
            userId, pickupLocation, destination, requestedPickupAt ?? DefaultPickupAt, passengerCount, considerationNotes);

    // U-047
    [Fact]
    public async Task ExecuteAsync_既定値_マッチング中の予約が保存される()
    {
        var user = SeedUser();

        var reservation = await Execute(user.Id);

        Assert.Equal(ReservationStatus.Matching, reservation.Status);
        Assert.Equal(user.Id, reservation.UserId);
        Assert.Equal("市役所前", reservation.PickupLocation);
        Assert.Equal("中央病院", reservation.Destination);
        Assert.Equal(DefaultPickupAt, reservation.RequestedPickupAt);
        Assert.Equal(2, reservation.PassengerCount);
        Assert.Equal("車椅子を使用", reservation.ConsiderationNotes);
        Assert.Null(reservation.CancellationReason);
        Assert.Null(reservation.CancelledAt);
        Assert.Same(reservation, Assert.Single(_reservations.Added));
    }

    // U-048
    [Fact]
    public async Task ExecuteAsync_既定値_予約番号がRR_日付_16進4桁の形式()
    {
        var user = SeedUser();

        var before = DateTime.UtcNow;
        var reservation = await Execute(user.Id);
        var after = DateTime.UtcNow;

        var match = Regex.Match(reservation.ReservationNumber, @"^RR-(\d{8})-[0-9A-F]{4}$");
        Assert.True(match.Success, reservation.ReservationNumber);
        // UTC の日付が変わる瞬間でも落ちないよう、前後どちらかと一致すればよい
        var datePart = match.Groups[1].Value;
        Assert.Contains(datePart, new[]
        {
            before.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            after.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        });
    }

    // U-049
    [Fact]
    public async Task ExecuteAsync_配慮事項なし_nullのまま保存される()
    {
        var user = SeedUser();

        var reservation = await Execute(user.Id, considerationNotes: null);

        Assert.Null(reservation.ConsiderationNotes);
        Assert.Single(_reservations.Added);
    }

    // U-050
    [Fact]
    public async Task ExecuteAsync_乗車人数1_保存される()
    {
        var user = SeedUser();

        await Execute(user.Id, passengerCount: 1);

        Assert.Single(_reservations.Added);
    }

    // U-051
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_乗車人数が1未満_ArgumentExceptionで保存されない(int passengerCount)
    {
        var user = SeedUser();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Execute(user.Id, passengerCount: passengerCount));

        Assert.Equal("passengerCount", ex.ParamName);
        Assert.Empty(_reservations.Added);
    }

    // U-052
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_乗車地が空_ArgumentExceptionで保存されない(string pickupLocation)
    {
        var user = SeedUser();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Execute(user.Id, pickupLocation: pickupLocation));

        Assert.Equal("pickupLocation", ex.ParamName);
        Assert.Empty(_reservations.Added);
    }

    // U-053
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_目的地が空_ArgumentExceptionで保存されない(string destination)
    {
        var user = SeedUser();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Execute(user.Id, destination: destination));

        Assert.Equal("destination", ex.ParamName);
        Assert.Empty(_reservations.Added);
    }

    // U-054
    [Fact]
    public async Task ExecuteAsync_未登録のユーザー_UserNotFoundExceptionで保存されない()
    {
        var userId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => Execute(userId));

        Assert.Equal(userId, ex.UserId);
        Assert.Empty(_reservations.Added);
    }

    // U-055
    [Fact]
    public async Task ExecuteAsync_Driverで稼働中_InvalidOperationExceptionで保存されない()
    {
        var user = SeedUser(TestUsers.Both(UserRole.Driver));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(user.Id));

        Assert.Empty(_reservations.Added);
    }

    // U-056
    [Fact]
    public async Task ExecuteAsync_Driverのみのユーザー_InvalidOperationExceptionで保存されない()
    {
        var user = SeedUser(TestUsers.Driver());

        await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(user.Id));

        Assert.Empty(_reservations.Added);
    }

    // U-057
    [Fact]
    public async Task ExecuteAsync_Driverのみかつ乗車人数0_稼働区分のエラーが先()
    {
        var user = SeedUser(TestUsers.Driver());

        await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(user.Id, passengerCount: 0));

        Assert.Empty(_reservations.Added);
    }

    // U-058
    [Fact]
    public async Task ExecuteAsync_過去の希望乗車日時_検証されず保存される()
    {
        var user = SeedUser();
        var past = DateTime.UtcNow.AddDays(-1);

        var reservation = await Execute(user.Id, requestedPickupAt: past);

        Assert.Equal(past, reservation.RequestedPickupAt);
        Assert.Single(_reservations.Added);
    }

    // U-059
    [Fact]
    public async Task ExecuteAsync_乗車地の前後に空白_トリムせず保存される()
    {
        var user = SeedUser();

        var reservation = await Execute(user.Id, pickupLocation: " 市役所前 ");

        Assert.Equal(" 市役所前 ", reservation.PickupLocation);
    }
}
