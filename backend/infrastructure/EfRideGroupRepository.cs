using Microsoft.EntityFrameworkCore;
using Usecase.RideGroup;
using DomainRideGroupStatus = Domain.RideGroups.RideGroupStatus;

namespace Infrastructure.RideGroups
{
    public class EfRideGroupRepository : IRideGroupRepository
    {
        private readonly AppDbContext _db;

        public EfRideGroupRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<bool> HasUnfinishedByDriverAsync(Guid driverId)
        {
            return _db.RideGroups.AnyAsync(g =>
                g.DriverId == driverId
                && (g.Status == DomainRideGroupStatus.Confirmed || g.Status == DomainRideGroupStatus.InProgress));
        }
    }
}
