using Microsoft.EntityFrameworkCore;
using Usecase.User;
using DomainUser = Domain.Users.User;

namespace Infrastructure.Users
{
    public class EfUserRepository : IUserRepository
    {
        private readonly AppDbContext _db;

        public EfUserRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<DomainUser?> FindByIdAsync(Guid id)
        {
            return _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public Task<bool> ExistsAsync(Guid id)
        {
            return _db.Users.AnyAsync(u => u.Id == id);
        }

        public async Task AddAsync(DomainUser user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
    }
}
