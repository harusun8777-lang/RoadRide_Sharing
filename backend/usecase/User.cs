using DomainUser = Domain.Users.User;
using UserRole = Domain.Users.UserRole;

namespace Usecase.User
{
    public interface IUserRepository
    {
        Task<DomainUser?> FindByIdAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
        Task AddAsync(DomainUser user);
    }

    public class UserNotFoundException : Exception
    {
        public Guid UserId { get; }

        public UserNotFoundException(Guid userId)
            : base($"User {userId} was not found.")
        {
            UserId = userId;
        }
    }

    public class RegisterUserUseCase
    {
        private readonly IUserRepository _userRepository;

        public RegisterUserUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<DomainUser> ExecuteAsync(
            string email,
            string lastName,
            string firstName,
            string kanaLastName,
            string kanaFirstName,
            UserRole role)
        {
            var user = DomainUser.Create(email, lastName, firstName, kanaLastName, kanaFirstName, role);
            await _userRepository.AddAsync(user);
            return user;
        }
    }

    public class GetUserUseCase
    {
        private readonly IUserRepository _userRepository;

        public GetUserUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<DomainUser> ExecuteAsync(Guid id)
        {
            return await _userRepository.FindByIdAsync(id)
                ?? throw new UserNotFoundException(id);
        }
    }
}
