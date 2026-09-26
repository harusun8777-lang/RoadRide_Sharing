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
            return _db.Users
                .Include(u => u.Rider)
                .Include(u => u.Driver)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public Task<DomainUser?> FindByEmailAsync(string email)
        {
            return _db.Users
                .Include(u => u.Rider)
                .Include(u => u.Driver)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task AddAsync(DomainUser user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(DomainUser user)
        {
            if (_db.Entry(user).State == EntityState.Detached)
                _db.Users.Update(user);

            await _db.SaveChangesAsync();
        }
    }
}
