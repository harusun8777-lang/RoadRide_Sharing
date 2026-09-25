using DomainReservation = Domain.Reservations.Reservation;
using ReservationStatus = Domain.Reservations.ReservationStatus;
using UserRole = Domain.Users.UserRole;
using IUserRepository = Usecase.User.IUserRepository;
using UserNotFoundException = Usecase.User.UserNotFoundException;

namespace Usecase.Reservation
{
    public record ReservationListFilter(
        Guid? UserId = null,
        DateOnly? Date = null,
        ReservationStatus? Status = null,
        DateTime? From = null,
        DateTime? To = null,
        int Page = 1,
        int Limit = 50);

    public class ReservationNotFoundException : Exception
    {
        public Guid ReservationId { get; }

        public ReservationNotFoundException(Guid reservationId)
            : base($"Reservation {reservationId} was not found.")
        {
            ReservationId = reservationId;
        }
    }

    public interface IReservationRepository
    {
        Task<DomainReservation?> FindByIdAsync(Guid id);
        Task<(IReadOnlyList<DomainReservation> Items, int Total)> ListAsync(ReservationListFilter filter);
        // 完了・キャンセル以外の予約が残っているか
        Task<bool> HasUnfinishedAsync(Guid userId);
        Task AddAsync(DomainReservation reservation);
        Task UpdateAsync(DomainReservation reservation);
    }

    public class RegisterReservationUseCase
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly IUserRepository _userRepository;

        public RegisterReservationUseCase(
            IReservationRepository reservationRepository,
            IUserRepository userRepository)
        {
            _reservationRepository = reservationRepository;
            _userRepository = userRepository;
        }

        public async Task<DomainReservation> ExecuteAsync(
            Guid userId,
            string pickupLocation,
            string destination,
            DateTime requestedPickupAt,
            int passengerCount,
            string? considerationNotes)
        {
            var user = await _userRepository.FindByIdAsync(userId)
                ?? throw new UserNotFoundException(userId);
            user.EnsureActiveAs(UserRole.Rider);

            var reservationNumber = GenerateReservationNumber();
            var reservation = DomainReservation.Create(
                reservationNumber,
                userId,
                pickupLocation,
                destination,
                requestedPickupAt,
                passengerCount,
                considerationNotes);

            await _reservationRepository.AddAsync(reservation);
            return reservation;
        }

        private static string GenerateReservationNumber()
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var suffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            return $"RR-{datePart}-{suffix}";
        }
    }

    public class ListReservationsUseCase
    {
        private readonly IReservationRepository _reservationRepository;

        public ListReservationsUseCase(IReservationRepository reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        public Task<(IReadOnlyList<DomainReservation> Items, int Total)> ExecuteAsync(ReservationListFilter filter)
        {
            return _reservationRepository.ListAsync(filter);
        }
    }

    public class GetReservationUseCase
    {
        private readonly IReservationRepository _reservationRepository;

        public GetReservationUseCase(IReservationRepository reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        public async Task<DomainReservation> ExecuteAsync(Guid id, Guid userId)
        {
            return await FindOwnedAsync(_reservationRepository, id, userId);
        }

        // 他人の予約は存在自体を知らせないため、見つからない扱いにする
        internal static async Task<DomainReservation> FindOwnedAsync(
            IReservationRepository repository, Guid id, Guid userId)
        {
            var reservation = await repository.FindByIdAsync(id);
            if (reservation is null || reservation.UserId != userId)
                throw new ReservationNotFoundException(id);
            return reservation;
        }
    }

    public class CancelReservationUseCase
    {
        private readonly IReservationRepository _reservationRepository;

        public CancelReservationUseCase(IReservationRepository reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        public async Task<DomainReservation> ExecuteAsync(Guid id, Guid userId, string? reason)
        {
            var reservation = await GetReservationUseCase.FindOwnedAsync(_reservationRepository, id, userId);

            reservation.Cancel(reason);
            await _reservationRepository.UpdateAsync(reservation);
            return reservation;
        }
    }
}
