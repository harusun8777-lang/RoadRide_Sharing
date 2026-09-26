using Backend.Tests.Infrastructure.Fixtures;
using Backend.Tests.TestDoubles;
using Domain.RideGroups;
using Infrastructure.RideGroups;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests.Infrastructure;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Database")]
public class EfRideGroupRepositoryTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private async Task<Guid> SeedDriverAsync() => (await SaveUserAsync(TestUsers.Driver())).Id;

    private async Task<bool> HasUnfinishedByDriverAsync(Guid driverId)
    {
        await using var db = CreateContext();
        return await new EfRideGroupRepository(db).HasUnfinishedByDriverAsync(driverId);
    }

    // I-130
    [Theory]
    [InlineData(RideGroupStatus.Confirmed)]
    [InlineData(RideGroupStatus.InProgress)]
    public async Task HasUnfinishedByDriverAsync_未完了の便が1件_true(RideGroupStatus status)
    {
        var driverId = await SeedDriverAsync();
        await SaveRideGroupAsync(driverId, status);

        Assert.True(await HasUnfinishedByDriverAsync(driverId));
    }

    // I-131
    [Fact]
    public async Task HasUnfinishedByDriverAsync_候補の便だけ_false()
    {
        var driverId = await SeedDriverAsync();
        await SaveRideGroupAsync(driverId, RideGroupStatus.Proposed);

        Assert.False(await HasUnfinishedByDriverAsync(driverId));
    }

    // I-132
    [Theory]
    [InlineData(RideGroupStatus.Completed)]
    [InlineData(RideGroupStatus.Cancelled)]
    public async Task HasUnfinishedByDriverAsync_完了かキャンセルの便だけ_false(RideGroupStatus status)
    {
        var driverId = await SeedDriverAsync();
        await SaveRideGroupAsync(driverId, status);

        Assert.False(await HasUnfinishedByDriverAsync(driverId));
    }

    // I-133
    [Fact]
    public async Task HasUnfinishedByDriverAsync_便のない運転手_false()
    {
        Assert.False(await HasUnfinishedByDriverAsync(await SeedDriverAsync()));
    }

    // I-134
    [Fact]
    public async Task HasUnfinishedByDriverAsync_他の運転手の便だけ確定_false()
    {
        var driverId = await SeedDriverAsync();
        var otherId = await SeedDriverAsync();
        await SaveRideGroupAsync(otherId, RideGroupStatus.Confirmed);

        Assert.False(await HasUnfinishedByDriverAsync(driverId));
    }

    // I-135
    [Fact]
    public async Task HasUnfinishedByDriverAsync_候補と運行中が混在_true()
    {
        var driverId = await SeedDriverAsync();
        await SaveRideGroupAsync(driverId, RideGroupStatus.Proposed);
        await SaveRideGroupAsync(driverId, RideGroupStatus.InProgress);

        Assert.True(await HasUnfinishedByDriverAsync(driverId));
    }

    // I-136
    [Fact]
    public async Task 状態の往復_候補の便を保存して別コンテキストで読む_全プロパティが一致する()
    {
        var driverId = await SeedDriverAsync();
        var saved = await SaveRideGroupAsync(driverId);

        await using var db = CreateContext();
        var read = await db.RideGroups.SingleAsync(g => g.Id == saved.Id);

        Assert.Equal(RideGroupStatus.Proposed, read.Status);
        Assert.Equal(saved.GroupNumber, read.GroupNumber);
        Assert.Equal(driverId, read.DriverId);
        Assert.Equal(saved.CreatedAt.Ticks, read.CreatedAt.Ticks);
        Assert.Equal(saved.UpdatedAt.Ticks, read.UpdatedAt.Ticks);
        Assert.Equal(DateTimeKind.Utc, read.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, read.UpdatedAt.Kind);
        Assert.Equal("proposed", await db.Database.SqlQuery<string>($"SELECT status AS Value FROM ride_groups").SingleAsync());
    }
}
