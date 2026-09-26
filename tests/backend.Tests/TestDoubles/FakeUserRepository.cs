using Usecase.User;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.TestDoubles
{
    // IUserRepository のインメモリ実装。受け取ったインスタンスの参照をそのまま保持する
    public class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, DomainUser> _users = new();

        public List<Guid> FindByIdCalls { get; } = new();
        public List<string> FindByEmailCalls { get; } = new();
        public List<DomainUser> Added { get; } = new();
        public List<DomainUser> Updated { get; } = new();

        public IReadOnlyCollection<DomainUser> All => _users.Values;

        // 呼び出し記録には残さずに事前登録する
        public void Seed(params DomainUser[] users)
        {
            foreach (var user in users)
                _users[user.Id] = user;
        }

        public DomainUser? Get(Guid id) => _users.GetValueOrDefault(id);

        public void Reset()
        {
            _users.Clear();
            FindByIdCalls.Clear();
            FindByEmailCalls.Clear();
            Added.Clear();
            Updated.Clear();
        }

        public Task<DomainUser?> FindByIdAsync(Guid id)
        {
            FindByIdCalls.Add(id);
            return Task.FromResult(_users.GetValueOrDefault(id));
        }

        public Task<DomainUser?> FindByEmailAsync(string email)
        {
            FindByEmailCalls.Add(email);
            return Task.FromResult(_users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.Ordinal)));
        }

        public Task AddAsync(DomainUser user)
        {
            Added.Add(user);
            _users[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainUser user)
        {
            Updated.Add(user);
            _users[user.Id] = user;
            return Task.CompletedTask;
        }
    }
}
