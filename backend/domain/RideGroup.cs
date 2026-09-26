namespace Domain.RideGroups
{
    public enum RideGroupStatus
    {
        Proposed,
        Confirmed,
        InProgress,
        Completed,
        Cancelled
    }

    // 1人の運転手が運ぶ乗合の便。候補（Proposed）は利用者が選ぶと Confirmed になる
    public class RideGroup
    {
        public Guid Id { get; }
        public string GroupNumber { get; }
        public Guid DriverId { get; }
        public RideGroupStatus Status { get; private set; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; private set; }

        private RideGroup(
            Guid id,
            string groupNumber,
            Guid driverId,
            RideGroupStatus status,
            DateTime createdAt,
            DateTime updatedAt)
        {
            Id = id;
            GroupNumber = groupNumber;
            DriverId = driverId;
            Status = status;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public static RideGroup Propose(string groupNumber, Guid driverId)
        {
            if (string.IsNullOrWhiteSpace(groupNumber))
                throw new ArgumentException("GroupNumber must not be empty.", nameof(groupNumber));

            var now = DateTime.UtcNow;
            return new RideGroup(Guid.NewGuid(), groupNumber, driverId, RideGroupStatus.Proposed, now, now);
        }
    }
}
