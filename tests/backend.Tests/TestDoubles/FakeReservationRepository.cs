using Usecase.Reservation;
using DomainReservation = Domain.Reservations.Reservation;

namespace Backend.Tests.TestDoubles
{
    // IReservationRepository のインメモリ実装。一覧の絞り込みは行わず、ListResult に設定した値を返す
    public class FakeReservationRepository : IReservationRepository
    {
        private readonly Dictionary<Guid, DomainReservation> _reservations = new();

        public List<Guid> FindByIdCalls { get; } = new();
        public List<ReservationListFilter> ListCalls { get; } = new();
        public List<Guid> HasUnfinishedCalls { get; } = new();
        public List<DomainReservation> Added { get; } = new();
        public List<DomainReservation> Updated { get; } = new();

        // ListAsync が返す値
        public (IReadOnlyList<DomainReservation> Items, int Total) ListResult { get; set; } = ([], 0);

        // HasUnfinishedAsync が true を返すユーザー
        public HashSet<Guid> UsersWithUnfinished { get; } = new();

        public ReservationListFilter? LastListFilter => ListCalls.LastOrDefault();

        public void Seed(params DomainReservation[] reservations)
        {
            foreach (var reservation in reservations)
                _reservations[reservation.Id] = reservation;
        }

        public DomainReservation? Get(Guid id) => _reservations.GetValueOrDefault(id);

        public void Reset()
        {
            _reservations.Clear();
            FindByIdCalls.Clear();
            ListCalls.Clear();
            HasUnfinishedCalls.Clear();
            Added.Clear();
            Updated.Clear();
            UsersWithUnfinished.Clear();
            ListResult = ([], 0);
        }

        public Task<DomainReservation?> FindByIdAsync(Guid id)
        {
            FindByIdCalls.Add(id);
            return Task.FromResult(_reservations.GetValueOrDefault(id));
        }

        public Task<(IReadOnlyList<DomainReservation> Items, int Total)> ListAsync(ReservationListFilter filter)
        {
            ListCalls.Add(filter);
            return Task.FromResult(ListResult);
        }

        public Task<bool> HasUnfinishedAsync(Guid userId)
        {
            HasUnfinishedCalls.Add(userId);
            return Task.FromResult(UsersWithUnfinished.Contains(userId));
        }

        public Task AddAsync(DomainReservation reservation)
        {
            Added.Add(reservation);
            _reservations[reservation.Id] = reservation;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainReservation reservation)
        {
            Updated.Add(reservation);
            _reservations[reservation.Id] = reservation;
            return Task.CompletedTask;
        }
    }
}
