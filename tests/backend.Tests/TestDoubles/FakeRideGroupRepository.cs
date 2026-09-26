using Usecase.RideGroup;

namespace Backend.Tests.TestDoubles
{
    public class FakeRideGroupRepository : IRideGroupRepository
    {
        public List<Guid> HasUnfinishedByDriverCalls { get; } = new();

        // HasUnfinishedByDriverAsync が true を返す運転手
        public HashSet<Guid> DriversWithUnfinished { get; } = new();

        public void Reset()
        {
            HasUnfinishedByDriverCalls.Clear();
            DriversWithUnfinished.Clear();
        }

        public Task<bool> HasUnfinishedByDriverAsync(Guid driverId)
        {
            HasUnfinishedByDriverCalls.Add(driverId);
            return Task.FromResult(DriversWithUnfinished.Contains(driverId));
        }
    }
}
