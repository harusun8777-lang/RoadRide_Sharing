namespace Domain.Users
{
    public enum UserRole
    {
        Rider,
        Dispatcher
    }

    public class User
    {
        public Guid Id { get; }
        public string Name { get; }
        public UserRole Role { get; }
        public DateTime CreatedAt { get; }

        private User(Guid id, string name, UserRole role, DateTime createdAt)
        {
            Id = id;
            Name = name;
            Role = role;
            CreatedAt = createdAt;
        }

        public static User Create(string name, UserRole role)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be empty.", nameof(name));

            return new User(Guid.NewGuid(), name, role, DateTime.UtcNow);
        }
    }
}