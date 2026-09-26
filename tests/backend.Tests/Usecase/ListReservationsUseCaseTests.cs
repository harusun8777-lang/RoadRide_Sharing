using Backend.Tests.TestDoubles;
using ListReservationsUseCase = Usecase.Reservation.ListReservationsUseCase;
using ReservationListFilter = Usecase.Reservation.ReservationListFilter;
using DomainReservation = Domain.Reservations.Reservation;
using ReservationStatus = Domain.Reservations.ReservationStatus;

namespace Backend.Tests.Usecase;

public class ListReservationsUseCaseTests
{
    private readonly FakeReservationRepository _reservations = new();
    private readonly ListReservationsUseCase _useCase;

    public ListReservationsUseCaseTests()
    {
        _useCase = new ListReservationsUseCase(_reservations);
    }

    // U-060
    [Fact]
    public async Task ExecuteAsync_すべての条件を指定_そのままリポジトリに渡し結果を返す()
    {
        var userId = Guid.NewGuid();
        var filter = new ReservationListFilter(
            UserId: userId,
            Date: new DateOnly(2026, 10, 1),
            Status: ReservationStatus.Confirmed,
            From: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            Page: 2,
            Limit: 20);
        IReadOnlyList<DomainReservation> items =
            [TestReservations.Matching(userId), TestReservations.Matching(userId)];
        _reservations.ListResult = (items, 5);

        var (resultItems, total) = await _useCase.ExecuteAsync(filter);

        Assert.Equal(filter, Assert.Single(_reservations.ListCalls));
        Assert.Same(items, resultItems);
        Assert.Equal(5, total);
    }

    // U-061
    [Fact]
    public async Task ExecuteAsync_0件_空の一覧と0が返る()
    {
        _reservations.ListResult = ([], 0);

        var (items, total) = await _useCase.ExecuteAsync(new ReservationListFilter(UserId: Guid.NewGuid()));

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    // U-062
    [Fact]
    public async Task ExecuteAsync_PageとLimitが0_補正せずに渡す()
    {
        await _useCase.ExecuteAsync(new ReservationListFilter(Page: 0, Limit: 0));

        var passed = Assert.Single(_reservations.ListCalls);
        Assert.Equal(0, passed.Page);
        Assert.Equal(0, passed.Limit);
    }
}
