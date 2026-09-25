using Microsoft.AspNetCore.Identity;
using DomainUser = Domain.Users.User;
using IPasswordHasher = Usecase.User.IPasswordHasher;

namespace Infrastructure.Auth
{
    // ASP.NET Core Identity の PasswordHasher（PBKDF2）を使う
    public class AspNetPasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<DomainUser> _hasher = new();

        public string Hash(string password) => _hasher.HashPassword(null!, password);

        public bool Verify(string passwordHash, string password) =>
            _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
    }
}
