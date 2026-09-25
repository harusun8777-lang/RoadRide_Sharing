using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Domain.Users
{
    public enum UserRole
    {
        Rider,
        Driver
    }

    // 利用者のマスタ。Rider / Driver のプロフィールを両方持てるが、稼働できるのは ActiveRole の1つだけ
    public class User
    {
        // 読み仮名は全角カタカナ（長音符を含む）のみ許可する
        private static readonly Regex KanaPattern = new(@"^[ァ-ヶー]+$", RegexOptions.Compiled);

        public Guid Id { get; }
        public string Email { get; }
        // 平文のパスワードは持たず、ハッシュ化済みの値だけを保持する
        public string PasswordHash { get; }
        public string LastName { get; }
        public string FirstName { get; }
        public string KanaLastName { get; }
        public string KanaFirstName { get; }
        public UserRole ActiveRole { get; private set; }
        public DateTime CreatedAt { get; }

        public Rider? Rider { get; private set; }
        public Driver? Driver { get; private set; }

        public string FullName => $"{LastName} {FirstName}";
        public string KanaFullName => $"{KanaLastName} {KanaFirstName}";

        public IEnumerable<UserRole> Roles =>
            Enum.GetValues<UserRole>().Where(HasRole);

        // EF Core はコンストラクタ引数をプロパティ名で対応付けるため、引数名はプロパティ名に合わせる
        private User(
            Guid id,
            string email,
            string passwordHash,
            string lastName,
            string firstName,
            string kanaLastName,
            string kanaFirstName,
            UserRole activeRole,
            DateTime createdAt)
        {
            Id = id;
            Email = email;
            PasswordHash = passwordHash;
            LastName = lastName;
            FirstName = firstName;
            KanaLastName = kanaLastName;
            KanaFirstName = kanaFirstName;
            ActiveRole = activeRole;
            CreatedAt = createdAt;
        }

        public static User Create(
            string email,
            string passwordHash,
            string lastName,
            string firstName,
            string kanaLastName,
            string kanaFirstName,
            UserRole initialRole)
        {
            // 引数は左から順に評価されるため、検証順は引数順と同じになる
            var user = new User(
                Guid.NewGuid(),
                RequireText(email),
                RequireText(passwordHash),
                RequireText(lastName),
                RequireText(firstName),
                RequireKana(kanaLastName),
                RequireKana(kanaFirstName),
                initialRole,
                DateTime.UtcNow
            );
            user.AddRole(initialRole);
            return user;
        }

        public bool HasRole(UserRole role) => role switch
        {
            UserRole.Rider => Rider is not null,
            UserRole.Driver => Driver is not null,
            _ => false
        };

        public void AddRole(UserRole role)
        {
            if (HasRole(role))
                throw new InvalidOperationException($"User already has the {role} role.");

            switch (role)
            {
                case UserRole.Rider: Rider = Rider.Create(Id); break;
                case UserRole.Driver: Driver = Driver.Create(Id); break;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        public void SwitchRole(UserRole role)
        {
            if (!HasRole(role))
                throw new InvalidOperationException($"User does not have the {role} role.");

            ActiveRole = role;
        }

        public void EnsureActiveAs(UserRole role)
        {
            if (ActiveRole != role)
                throw new InvalidOperationException($"User is not active as {role}.");
        }

        // 空文字・空白のみを拒否し、前後の空白を除いた値を返す
        private static string RequireText(string? value, [CallerArgumentExpression(nameof(value))] string paramName = "")
            => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException($"{paramName} must not be empty.", paramName)
                : value.Trim();

        private static string RequireKana(string? value, [CallerArgumentExpression(nameof(value))] string paramName = "")
            => value is not null && KanaPattern.IsMatch(value)
                ? value
                : throw new ArgumentException($"{paramName} must be full-width katakana.", paramName);
    }
}
