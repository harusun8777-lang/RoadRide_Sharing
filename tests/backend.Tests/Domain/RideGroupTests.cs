using global::Domain.RideGroups;

namespace Backend.Tests.Domain
{
    public class RideGroupTests
    {
        private const string GroupNumber = "G-0001";

        // D-077
        [Fact]
        public void Propose_既定値_Proposedで入力どおりに生成される()
        {
            var driverId = Guid.NewGuid();

            var group = RideGroup.Propose(GroupNumber, driverId);

            Assert.Equal(RideGroupStatus.Proposed, group.Status);
            Assert.Equal(GroupNumber, group.GroupNumber);
            Assert.Equal(driverId, group.DriverId);
            Assert.NotEqual(Guid.Empty, group.Id);
        }

        // D-078
        [Fact]
        public void Propose_既定値_CreatedAtとUpdatedAtが同じで呼び出し前後のUtc()
        {
            var before = DateTime.UtcNow;
            var group = RideGroup.Propose(GroupNumber, Guid.NewGuid());
            var after = DateTime.UtcNow;

            Assert.Equal(group.CreatedAt, group.UpdatedAt);
            Assert.InRange(group.CreatedAt, before, after);
            Assert.Equal(DateTimeKind.Utc, group.CreatedAt.Kind);
        }

        // D-079
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Propose_groupNumberが空_ArgumentException(string? groupNumber)
        {
            var ex = Assert.Throws<ArgumentException>(() => RideGroup.Propose(groupNumber!, Guid.NewGuid()));
            Assert.Equal("groupNumber", ex.ParamName);
        }

        // D-080
        [Fact]
        public void Propose_groupNumberの前後に空白_Trimされずに保持される()
        {
            var group = RideGroup.Propose(" G-0001 ", Guid.NewGuid());
            Assert.Equal(" G-0001 ", group.GroupNumber);
        }

        // D-081
        [Fact]
        public void Propose_driverIdがGuidEmpty_例外なし()
        {
            var group = RideGroup.Propose(GroupNumber, Guid.Empty);
            Assert.Equal(Guid.Empty, group.DriverId);
        }

        // D-082
        [Fact]
        public void Propose_2回生成_Idが異なる()
        {
            var driverId = Guid.NewGuid();
            Assert.NotEqual(RideGroup.Propose(GroupNumber, driverId).Id, RideGroup.Propose(GroupNumber, driverId).Id);
        }
    }
}
