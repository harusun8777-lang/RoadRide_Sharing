using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Domain.Users
{
    public enum UserRole
    {
        Rider,
        Dispatcher
    }

    public class User
    {
        // 読み仮名は全角カタカナ（長音符を含む）のみ許可する
        private static readonly Regex KanaPattern = new(@"^[ァ-ヶー]+$", RegexOptions.Compiled);

        public Guid Id { get; }
        public string Email { get; }
        public string LastName { get; }
        public string FirstName { get; }
        public string KanaLastName { get; }
        public string KanaFirstName { get; }
        public UserRole Role { get; }
        public DateTime CreatedAt { get; }

        public string FullName => $"{LastName} {FirstName}";
        public string KanaFullName => $"{KanaLastName} {KanaFirstName}";

        // EF Core はコンストラクタ引数をプロパティ名で対応付けるため、引数名はプロパティ名に合わせる
        private User(
            Guid id,
            string email,
            string lastName,
            string firstName,
            string kanaLastName,
            string kanaFirstName,
            UserRole role,
            DateTime createdAt)
        {
            Id = id;
            Email = email;
            LastName = lastName;
            FirstName = firstName;
            KanaLastName = kanaLastName;
            KanaFirstName = kanaFirstName;
            Role = role;
            CreatedAt = createdAt;
        }

        public static User Create(
            string email,
            string lastName,
            string firstName,
            string kanaLastName,
            string kanaFirstName,
            UserRole role)
        {
            // 引数は左から順に評価されるため、検証順は引数順と同じになる
            return new User(
                Guid.NewGuid(),
                RequireText(email),
                RequireText(lastName),
                RequireText(firstName),
                RequireKana(kanaLastName),
                RequireKana(kanaFirstName),
                role,
                DateTime.UtcNow
            );
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
