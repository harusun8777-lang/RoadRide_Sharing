using Backend.Tests.TestDoubles;
using GetReservationUseCase = Usecase.Reservation.GetReservationUseCase;
using ReservationNotFoundException = Usecase.Reservation.ReservationNotFoundException;
using ReservationStatus = Domain.Reservations.ReservationStatus;

namespace Backend.Tests.Usecase;

public class GetReservationUseCaseTests
{
    private readonly FakeReservationRepository _reservations = new();
    private readonly GetReservationUseCase _useCase;

    public GetReservationUseCaseTests()
    {
        _useCase = new GetReservationUseCase(_reservations);
    }

    // U-063
    [Fact]
    public async Task ExecuteAsync_自分の予約_その予約が返る()
    {
        var userId = Guid.NewGuid();
        var reservation = TestReservations.Matching(userId);
        _reservations.Seed(reservation);

        var result = await _useCase.ExecuteAsync(reservation.Id, userId);

        Assert.Same(reservation, result);
    }

    // U-064
    [Fact]
    public async Task ExecuteAsync_存在しない予約_ReservationNotFoundException()
    {
        var id = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _useCase.ExecuteAsync(id, Guid.NewGuid()));

        Assert.Equal(id, ex.ReservationId);
    }

    // U-065
    [Fact]
    public async Task ExecuteAsync_他人の予約_存在しない予約と同じ例外になる()
    {
        var userId = Guid.NewGuid();
        var others = TestReservations.Matching(Guid.NewGuid());
        _reservations.Seed(others);
        var missingId = Guid.NewGuid();

        var notFound = await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _useCase.ExecuteAsync(missingId, userId));
        var notOwned = await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _useCase.ExecuteAsync(others.Id, userId));

        Assert.Equal(others.Id, notOwned.ReservationId);
        // Id 部分を除けばメッセージは同じ
        Assert.Equal(
            notFound.Message.Replace(missingId.ToString(), "{id}"),
            notOwned.Message.Replace(others.Id.ToString(), "{id}"));
    }

    // U-066
    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Completed)]
    public async Task ExecuteAsync_終了済みの自分の予約_その予約が返る(ReservationStatus status)
    {
        var userId = Guid.NewGuid();
        var reservation = TestReservations.InStatus(status, userId);
        _reservations.Seed(reservation);

        var result = await _useCase.ExecuteAsync(reservation.Id, userId);

        Assert.Same(reservation, result);
    }
}
