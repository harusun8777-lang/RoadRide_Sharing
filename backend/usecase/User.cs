using DomainUser = Domain.Users.User;
using UserRole = Domain.Users.UserRole;
using IReservationRepository = Usecase.Reservation.IReservationRepository;
using IRideGroupRepository = Usecase.RideGroup.IRideGroupRepository;

namespace Usecase.User
{
    public interface IUserRepository
    {
        Task<DomainUser?> FindByIdAsync(Guid id);
        Task<DomainUser?> FindByEmailAsync(string email);
        Task AddAsync(DomainUser user);
        Task UpdateAsync(DomainUser user);
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

    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string passwordHash, string password);
    }

    public class EmailAlreadyRegisteredException : Exception
    {
        public string Email { get; }

        public EmailAlreadyRegisteredException(string email)
            : base($"Email {email} is already registered.")
        {
            Email = email;
        }
    }

    public class RegisterUserUseCase
    {
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 128;

        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        public RegisterUserUseCase(IUserRepository userRepository, IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<DomainUser> ExecuteAsync(
            string email,
            string password,
            string lastName,
            string firstName,
            string kanaLastName,
            string kanaFirstName,
            UserRole role)
        {
            if (password is null || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
                throw new ArgumentException(
                    $"password must be {MinPasswordLength} to {MaxPasswordLength} characters.", nameof(password));

            if (!string.IsNullOrWhiteSpace(email) && await _userRepository.FindByEmailAsync(email.Trim()) is not null)
                throw new EmailAlreadyRegisteredException(email.Trim());

            var user = DomainUser.Create(
                email, _passwordHasher.Hash(password), lastName, firstName, kanaLastName, kanaFirstName, role);
            await _userRepository.AddAsync(user);
            return user;
        }
    }

    // JWT の sub（ユーザーID）から、ログイン中のユーザーを引く。見つからなければ null
    public class GetCurrentUserUseCase
    {
        private readonly IUserRepository _userRepository;

        public GetCurrentUserUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public Task<DomainUser?> ExecuteAsync(string? subject)
        {
            return Guid.TryParse(subject, out var id)
                ? _userRepository.FindByIdAsync(id)
                : Task.FromResult<DomainUser?>(null);
        }
    }

    public class AddUserRoleUseCase
    {
        private readonly IUserRepository _userRepository;

        public AddUserRoleUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<DomainUser> ExecuteAsync(Guid id, UserRole role)
        {
            var user = await _userRepository.FindByIdAsync(id)
                ?? throw new UserNotFoundException(id);

            user.AddRole(role);
            await _userRepository.UpdateAsync(user);
            return user;
        }
    }

    // 未完了の予約（Rider）または運行（Driver）が残っているため、稼働区分を切り替えられない
    public class UnfinishedActivityExistsException : InvalidOperationException
    {
        public Guid UserId { get; }
        public UserRole ActiveRole { get; }

        public UnfinishedActivityExistsException(Guid userId, UserRole activeRole)
            : base($"User {userId} has unfinished activities as {activeRole}.")
        {
            UserId = userId;
            ActiveRole = activeRole;
        }
    }

    public class SwitchUserRoleUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly IRideGroupRepository _rideGroupRepository;

        public SwitchUserRoleUseCase(
            IUserRepository userRepository,
            IReservationRepository reservationRepository,
            IRideGroupRepository rideGroupRepository)
        {
            _userRepository = userRepository;
            _reservationRepository = reservationRepository;
            _rideGroupRepository = rideGroupRepository;
        }

        public async Task<DomainUser> ExecuteAsync(Guid id, UserRole role)
        {
            var user = await _userRepository.FindByIdAsync(id)
                ?? throw new UserNotFoundException(id);

            // 別の区分へ移るときは、今の区分での予約・運行が完了かキャンセルになるまで切り替えさせない
            if (user.ActiveRole != role && await HasUnfinishedAsync(id, user.ActiveRole))
                throw new UnfinishedActivityExistsException(id, user.ActiveRole);

            user.SwitchRole(role);
            await _userRepository.UpdateAsync(user);
            return user;
        }

        private Task<bool> HasUnfinishedAsync(Guid id, UserRole activeRole) => activeRole switch
        {
            UserRole.Rider => _reservationRepository.HasUnfinishedAsync(id),
            UserRole.Driver => _rideGroupRepository.HasUnfinishedByDriverAsync(id),
            _ => throw new ArgumentOutOfRangeException(nameof(activeRole))
        };
    }
}
