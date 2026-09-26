using DomainUser = Domain.Users.User;
using IPasswordHasher = Usecase.User.IPasswordHasher;
using IUserRepository = Usecase.User.IUserRepository;

namespace Usecase.Auth
{
    public record AccessToken(string Token, DateTime ExpiresAt);

    public interface IAccessTokenIssuer
    {
        AccessToken Issue(DomainUser user);
    }

    // メールアドレスとパスワードのどちらが違うかは区別しない
    public class InvalidCredentialsException : Exception
    {
        public InvalidCredentialsException()
            : base("Email or password is incorrect.")
        {
        }
    }

    public class LoginUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IAccessTokenIssuer _tokenIssuer;

        public LoginUseCase(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IAccessTokenIssuer tokenIssuer)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenIssuer = tokenIssuer;
        }

        public async Task<AccessToken> ExecuteAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
                throw new InvalidCredentialsException();

            var user = await _userRepository.FindByEmailAsync(email.Trim());
            if (user is null || !_passwordHasher.Verify(user.PasswordHash, password))
                throw new InvalidCredentialsException();

            return _tokenIssuer.Issue(user);
        }
    }
}
