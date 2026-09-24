using Microsoft.EntityFrameworkCore;
using Usecase.Reservation;
using DomainReservation = Domain.Reservations.Reservation;

namespace Infrastructure.Reservations
{
    public class EfReservationRepository : IReservationRepository
    {
        // サービス提供地域（日本）のUTCオフセット。夏時間が無いため固定値で扱う
        private static readonly TimeSpan ServiceUtcOffset = TimeSpan.FromHours(9);

        private readonly AppDbContext _db;

        public EfReservationRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<DomainReservation?> FindByIdAsync(Guid id)
        {
            return _db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<(IReadOnlyList<DomainReservation> Items, int Total)> ListAsync(ReservationListFilter filter)
        {
            IQueryable<DomainReservation> query = _db.Reservations.AsNoTracking();

            if (filter.UserId is { } userId)
                query = query.Where(r => r.UserId == userId);

            if (filter.Status is { } status)
                query = query.Where(r => r.Status == status);

            if (filter.Date is { } date)
            {
                // 乗車日は日本時間の暦日として扱う
                var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ServiceUtcOffset).UtcDateTime;
                var dayEnd = dayStart.AddDays(1);
                query = query.Where(r => r.RequestedPickupAt >= dayStart && r.RequestedPickupAt < dayEnd);
            }

            if (filter.From is { } from)
                query = query.Where(r => r.RequestedPickupAt >= from);

            if (filter.To is { } to)
                query = query.Where(r => r.RequestedPickupAt <= to);

            var total = await query.CountAsync();

            var page = filter.Page < 1 ? 1 : filter.Page;
            var limit = filter.Limit < 1 ? 50 : filter.Limit;

            var items = await query
                .OrderBy(r => r.RequestedPickupAt)
                .ThenBy(r => r.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return (items, total);
        }

        public async Task AddAsync(DomainReservation reservation)
        {
            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(DomainReservation reservation)
        {
            if (_db.Entry(reservation).State == EntityState.Detached)
                _db.Reservations.Update(reservation);

            await _db.SaveChangesAsync();
        }
    }
}
