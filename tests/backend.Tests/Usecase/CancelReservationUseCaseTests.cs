using Backend.Tests.TestDoubles;
using CancelReservationUseCase = Usecase.Reservation.CancelReservationUseCase;
using ReservationNotFoundException = Usecase.Reservation.ReservationNotFoundException;
using DomainReservation = Domain.Reservations.Reservation;
using ReservationStatus = Domain.Reservations.ReservationStatus;

namespace Backend.Tests.Usecase;

public class CancelReservationUseCaseTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeReservationRepository _reservations = new();
    private readonly CancelReservationUseCase _useCase;

    public CancelReservationUseCaseTests()
    {
        _useCase = new CancelReservationUseCase(_reservations);
    }

    private DomainReservation SeedOwn(ReservationStatus status)
    {
        var reservation = TestReservations.InStatus(status, _userId);
        _reservations.Seed(reservation);
        return reservation;
    }

    // U-067
    [Fact]
    public async Task ExecuteAsync_マッチング中の予約_キャンセルになり保存される()
    {
        var reservation = SeedOwn(ReservationStatus.Matching);

        var before = DateTime.UtcNow;
        var result = await _useCase.ExecuteAsync(reservation.Id, _userId, "予定変更");
        var after = DateTime.UtcNow;

        Assert.Equal(ReservationStatus.Cancelled, result.Status);
        Assert.Equal("予定変更", result.CancellationReason);
        Assert.NotNull(result.CancelledAt);
        Assert.InRange(result.CancelledAt.Value, before, after);
        Assert.Same(reservation, Assert.Single(_reservations.Updated));
    }

    // U-068
    [Fact]
    public async Task ExecuteAsync_確定済みの予約_キャンセルになり保存される()
    {
        var reservation = SeedOwn(ReservationStatus.Confirmed);

        var result = await _useCase.ExecuteAsync(reservation.Id, _userId, "予定変更");

        Assert.Equal(ReservationStatus.Cancelled, result.Status);
        Assert.Single(_reservations.Updated);
    }

    // U-069
    [Fact]
    public async Task ExecuteAsync_理由なし_理由がnullのまま保存される()
    {
        var reservation = SeedOwn(ReservationStatus.Matching);

        var result = await _useCase.ExecuteAsync(reservation.Id, _userId, null);

        Assert.Null(result.CancellationReason);
        Assert.Single(_reservations.Updated);
    }

    // U-070
    [Fact]
    public async Task ExecuteAsync_乗車中の予約_InvalidOperationExceptionで保存されない()
    {
        var reservation = SeedOwn(ReservationStatus.InProgress);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _useCase.ExecuteAsync(reservation.Id, _userId, "予定変更"));

        Assert.Equal(ReservationStatus.InProgress, reservation.Status);
        Assert.Empty(_reservations.Updated);
    }

    // U-071
    [Fact]
    public async Task ExecuteAsync_完了した予約_InvalidOperationExceptionで保存されない()
    {
        var reservation = SeedOwn(ReservationStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _useCase.ExecuteAsync(reservation.Id, _userId, "予定変更"));

        Assert.Empty(_reservations.Updated);
    }

    // U-072
    [Fact]
    public async Task ExecuteAsync_キャンセル済みの予約_最初のキャンセル内容のまま保存されない()
    {
        var reservation = SeedOwn(ReservationStatus.Cancelled);
        var reason = reservation.CancellationReason;
        var cancelledAt = reservation.CancelledAt;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _useCase.ExecuteAsync(reservation.Id, _userId, "二回目"));

        Assert.Equal(reason, reservation.CancellationReason);
        Assert.Equal(cancelledAt, reservation.CancelledAt);
        Assert.Empty(_reservations.Updated);
    }

    // U-073
    [Fact]
    public async Task ExecuteAsync_存在しない予約_ReservationNotFoundExceptionで保存されない()
    {
        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _useCase.ExecuteAsync(Guid.NewGuid(), _userId, "予定変更"));

        Assert.Empty(_reservations.Updated);
    }

    // U-074
    [Fact]
    public async Task ExecuteAsync_他人の予約_ReservationNotFoundExceptionで状態も変わらない()
    {
        var others = TestReservations.Matching(Guid.NewGuid());
        _reservations.Seed(others);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _useCase.ExecuteAsync(others.Id, _userId, "予定変更"));

        Assert.Equal(ReservationStatus.Matching, others.Status);
        Assert.Empty(_reservations.Updated);
    }
}
