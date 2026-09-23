using System.Collections.Concurrent;
using Usecase.User;
using DomainUser = Domain.Users.User;

namespace Infrastructure.Users
{
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly ConcurrentDictionary<Guid, DomainUser> _store = new();

        public Task<DomainUser?> FindByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        public Task AddAsync(DomainUser user)
        {
            _store[user.Id] = user;
            return Task.CompletedTask;
        }
    }
}
