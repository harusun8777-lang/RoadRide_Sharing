using Domain.Reservations;
using Domain.RideGroups;
using Infrastructure;
using Infrastructure.Reservations;
using Infrastructure.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Infrastructure.Fixtures;

// テストごとに test_{Guid:N} の DB を作り、終わったら消す
public abstract class DatabaseTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    protected SqlServerFixture Fixture { get; } = fixture;
    protected string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        ConnectionString = Fixture.CreateDatabaseConnectionString();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    // 失敗をすぐ検出するため EnableRetryOnFailure は付けない
    protected AppDbContext CreateContext() => CreateContext(ConnectionString);

    protected static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);

    protected async Task<DomainUser> SaveUserAsync(DomainUser user)
    {
        await using var db = CreateContext();
        await new EfUserRepository(db).AddAsync(user);
        return user;
    }

    protected async Task<Reservation> SaveReservationAsync(Reservation reservation)
    {
        await using var db = CreateContext();
        await new EfReservationRepository(db).AddAsync(reservation);
        return reservation;
    }

    // 便はリポジトリに追加メソッドがないため DbContext に直接保存し、状態は ExecuteUpdate で変える
    protected async Task<RideGroup> SaveRideGroupAsync(Guid driverId, RideGroupStatus status = RideGroupStatus.Proposed, string? groupNumber = null)
    {
        var group = RideGroup.Propose(groupNumber ?? NewNumber("RG"), driverId);
        await using var db = CreateContext();
        db.RideGroups.Add(group);
        await db.SaveChangesAsync();
        if (status != RideGroupStatus.Proposed)
            await db.RideGroups.Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, status));
        return group;
    }

    // 予約番号の重複で失敗しないよう、十分に長い一意の番号で予約を作る
    protected static Reservation NewReservation(Guid userId, DateTime requestedPickupAt, string? considerationNotes = "車椅子を使用")
        => Reservation.Create(NewNumber("RR"), userId, "市役所前", "中央病院", requestedPickupAt, 2, considerationNotes);

    // 32 文字以内の一意な番号
    protected static string NewNumber(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    protected static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    // 例外の内側から SqlException のエラー番号を取り出す
    protected static int SqlErrorNumber(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is SqlException sql)
                return sql.Number;
        throw new Xunit.Sdk.XunitException($"SqlException が含まれていない: {ex}");
    }
}
