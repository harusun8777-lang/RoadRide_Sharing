using Usecase.User;

namespace Backend.Tests.TestDoubles
{
    // Hash(p) は "hashed:" + p を返す。Verify はその形式と一致するかだけを見る
    public class FakePasswordHasher : IPasswordHasher
    {
        public const string Prefix = "hashed:";

        public List<string> HashCalls { get; } = new();
        public List<(string PasswordHash, string Password)> VerifyCalls { get; } = new();

        public string Hash(string password)
        {
            HashCalls.Add(password);
            return Prefix + password;
        }

        public bool Verify(string passwordHash, string password)
        {
            VerifyCalls.Add((passwordHash, password));
            return passwordHash == Prefix + password;
        }
    }
}
