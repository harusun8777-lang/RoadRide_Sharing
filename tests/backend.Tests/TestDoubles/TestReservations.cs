using Domain.Reservations;

namespace Backend.Tests.TestDoubles
{
    // テスト用の予約を作る。ドメインの公開メソッドで状態を進めるため、実装どおりの遷移になる
    public static class TestReservations
    {
        public static readonly DateTime DefaultPickupAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        public static Reservation Matching(Guid userId, DateTime? requestedPickupAt = null)
            => Reservation.Create(
                $"RR-20260926-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                userId,
                "市役所前",
                "中央病院",
                requestedPickupAt ?? DefaultPickupAt,
                2,
                "車椅子を使用");

        public static Reservation InStatus(ReservationStatus status, Guid userId, DateTime? requestedPickupAt = null)
        {
            var reservation = Matching(userId, requestedPickupAt);
            switch (status)
            {
                case ReservationStatus.Matching:
                    break;
                case ReservationStatus.Confirmed:
                    reservation.Confirm();
                    break;
                case ReservationStatus.InProgress:
                    reservation.Confirm();
                    reservation.StartInProgress();
                    break;
                case ReservationStatus.Completed:
                    reservation.Confirm();
                    reservation.StartInProgress();
                    reservation.Complete();
                    break;
                case ReservationStatus.Cancelled:
                    reservation.Cancel("テスト用のキャンセル");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
            return reservation;
        }
    }
}
