using System.Collections.Concurrent;
using Usecase.Reservation;
using DomainReservation = Domain.Reservations.Reservation;

namespace Infrastructure.Reservations
{
    public class InMemoryReservationRepository : IReservationRepository
    {
        private readonly ConcurrentDictionary<Guid, DomainReservation> _store = new();

        public Task<DomainReservation?> FindByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var reservation);
            return Task.FromResult(reservation);
        }

        public Task<(IReadOnlyList<DomainReservation> Items, int Total)> ListAsync(ReservationListFilter filter)
        {
            IEnumerable<DomainReservation> query = _store.Values;

            if (filter.UserId is { } userId)
                query = query.Where(r => r.UserId == userId);

            if (filter.Status is { } status)
                query = query.Where(r => r.Status == status);

            if (filter.Date is { } date)
                query = query.Where(r => DateOnly.FromDateTime(r.RequestedPickupAt) == date);

            if (filter.From is { } from)
                query = query.Where(r => r.RequestedPickupAt >= from);

            if (filter.To is { } to)
                query = query.Where(r => r.RequestedPickupAt <= to);

            var ordered = query.OrderBy(r => r.RequestedPickupAt).ToList();
            var total = ordered.Count;

            var page = filter.Page < 1 ? 1 : filter.Page;
            var limit = filter.Limit < 1 ? 50 : filter.Limit;

            var items = ordered
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToList();

            return Task.FromResult<(IReadOnlyList<DomainReservation> Items, int Total)>((items, total));
        }

        public Task AddAsync(DomainReservation reservation)
        {
            _store[reservation.Id] = reservation;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainReservation reservation)
        {
            _store[reservation.Id] = reservation;
            return Task.CompletedTask;
        }
    }
}
