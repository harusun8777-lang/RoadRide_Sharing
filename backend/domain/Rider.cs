namespace Domain.Users
{
    // 利用者（乗客）としてのプロフィール。User 集約を通してのみ生成する
    public class Rider
    {
        public Guid UserId { get; }
        public DateTime CreatedAt { get; }

        private Rider(Guid userId, DateTime createdAt)
        {
            UserId = userId;
            CreatedAt = createdAt;
        }

        internal static Rider Create(Guid userId) => new(userId, DateTime.UtcNow);
    }
}
