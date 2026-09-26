namespace Domain.Users
{
    // 運転手としてのプロフィール。User 集約を通してのみ生成する
    public class Driver
    {
        public Guid UserId { get; }
        public DateTime CreatedAt { get; }

        private Driver(Guid userId, DateTime createdAt)
        {
            UserId = userId;
            CreatedAt = createdAt;
        }

        internal static Driver Create(Guid userId) => new(userId, DateTime.UtcNow);
    }
}
