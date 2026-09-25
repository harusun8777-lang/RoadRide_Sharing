using DomainUser = Domain.Users.User;
using UserRole = Domain.Users.UserRole;
using IReservationRepository = Usecase.Reservation.IReservationRepository;

namespace Usecase.User
{
    public interface IUserRepository
    {
        Task<DomainUser?> FindByIdAsync(Guid id);
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

    public class UnfinishedReservationExistsException : InvalidOperationException
    {
        public Guid UserId { get; }

        public UnfinishedReservationExistsException(Guid userId)
            : base($"User {userId} has unfinished reservations.")
        {
            UserId = userId;
        }
    }

    public class SwitchUserRoleUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IReservationRepository _reservationRepository;

        public SwitchUserRoleUseCase(
            IUserRepository userRepository,
            IReservationRepository reservationRepository)
        {
            _userRepository = userRepository;
            _reservationRepository = reservationRepository;
        }

        public async Task<DomainUser> ExecuteAsync(Guid id, UserRole role)
        {
            var user = await _userRepository.FindByIdAsync(id)
                ?? throw new UserNotFoundException(id);

            // Rider から離れるときは、予約が完了かキャンセルになるまで切り替えさせない
            if (user.ActiveRole == UserRole.Rider
                && role != UserRole.Rider
                && await _reservationRepository.HasUnfinishedAsync(id))
                throw new UnfinishedReservationExistsException(id);

            user.SwitchRole(role);
            await _userRepository.UpdateAsync(user);
            return user;
        }
    }
}
