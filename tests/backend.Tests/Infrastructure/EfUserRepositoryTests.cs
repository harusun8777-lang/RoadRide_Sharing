using Backend.Tests.Infrastructure.Fixtures;
using Backend.Tests.TestDoubles;
using Domain.Users;
using Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Infrastructure;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Database")]
public class EfUserRepositoryTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private async Task<int> CountAsync(string table)
    {
        await using var db = CreateContext();
        // テーブル名はテスト内の固定値だけを渡す
        return await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM {table}").SingleAsync();
    }

    private async Task<DomainUser?> FindByIdAsync(Guid id)
    {
        await using var db = CreateContext();
        return await new EfUserRepository(db).FindByIdAsync(id);
    }

    private async Task<DomainUser?> FindByEmailAsync(string email)
    {
        await using var db = CreateContext();
        return await new EfUserRepository(db).FindByEmailAsync(email);
    }

    // I-076
    [Fact]
    public async Task AddAsync_Riderで登録_usersとridersに1行ずつ()
    {
        await SaveUserAsync(TestUsers.Rider());

        Assert.Equal(1, await CountAsync("users"));
        Assert.Equal(1, await CountAsync("riders"));
        Assert.Equal(0, await CountAsync("drivers"));
    }

    // I-077
    [Fact]
    public async Task AddAsync_Driverで登録_usersとdriversに1行ずつ()
    {
        await SaveUserAsync(TestUsers.Driver());

        Assert.Equal(1, await CountAsync("users"));
        Assert.Equal(1, await CountAsync("drivers"));
        Assert.Equal(0, await CountAsync("riders"));
    }

    // I-078
    [Fact]
    public async Task FindByIdAsync_保存したRider_全プロパティが復元される()
    {
        var user = await SaveUserAsync(TestUsers.Rider());

        var read = await FindByIdAsync(user.Id);

        Assert.NotNull(read);
        Assert.Equal(user.Id, read.Id);
        Assert.Equal(user.Email, read.Email);
        Assert.Equal(user.PasswordHash, read.PasswordHash);
        Assert.Equal(user.LastName, read.LastName);
        Assert.Equal(user.FirstName, read.FirstName);
        Assert.Equal(user.KanaLastName, read.KanaLastName);
        Assert.Equal(user.KanaFirstName, read.KanaFirstName);
        Assert.Equal(UserRole.Rider, read.ActiveRole);
        Assert.Equal(user.CreatedAt.Ticks, read.CreatedAt.Ticks);
        Assert.Equal(DateTimeKind.Utc, read.CreatedAt.Kind);
        Assert.NotNull(read.Rider);
        Assert.Equal(user.Id, read.Rider.UserId);
        Assert.Null(read.Driver);
        Assert.Equal([UserRole.Rider], read.Roles);
        Assert.Equal("山田 太郎", read.FullName);
    }

    // I-079
    [Fact]
    public async Task FindByIdAsync_両方のプロフィール_RiderとDriverが読み込まれる()
    {
        var user = await SaveUserAsync(TestUsers.Both(UserRole.Rider));

        var read = await FindByIdAsync(user.Id);

        Assert.NotNull(read);
        Assert.NotNull(read.Rider);
        Assert.NotNull(read.Driver);
        Assert.True(read.HasRole(UserRole.Rider));
        Assert.True(read.HasRole(UserRole.Driver));
    }

    // I-080
    [Fact]
    public async Task FindByIdAsync_存在しないID_null()
    {
        await SaveUserAsync(TestUsers.Rider());

        Assert.Null(await FindByIdAsync(Guid.NewGuid()));
    }

    // I-081
    [Fact]
    public async Task FindByEmailAsync_登録済みのメール_ユーザーとプロフィールが返る()
    {
        var user = await SaveUserAsync(TestUsers.Both(UserRole.Driver, email: "user@example.com"));

        var read = await FindByEmailAsync("user@example.com");

        Assert.NotNull(read);
        Assert.Equal(user.Id, read.Id);
        Assert.NotNull(read.Rider);
        Assert.NotNull(read.Driver);
        Assert.Equal(UserRole.Driver, read.ActiveRole);
    }

    // I-082
    [Fact]
    public async Task FindByEmailAsync_未登録のメール_null()
    {
        await SaveUserAsync(TestUsers.Rider(email: "user@example.com"));

        Assert.Null(await FindByEmailAsync("other@example.com"));
    }

    // I-083
    [Fact]
    public async Task FindByEmailAsync_大文字小文字が違う_見つかる()
    {
        var user = await SaveUserAsync(TestUsers.Rider(email: "user@example.com"));

        // 照合順序が _CI_ のため（I-060 が前提）
        Assert.Equal(user.Id, (await FindByEmailAsync("USER@example.com"))?.Id);
    }

    // I-084
    [Fact]
    public async Task FindByEmailAsync_末尾に空白_見つかる()
    {
        var user = await SaveUserAsync(TestUsers.Rider(email: "user@example.com"));

        // SQL Server の = 比較は末尾の空白を無視する
        Assert.Equal(user.Id, (await FindByEmailAsync("user@example.com "))?.Id);
    }

    // I-085
    [Fact]
    public async Task AddAsync_メールの重複_DbUpdateExceptionで2601()
    {
        await SaveUserAsync(TestUsers.Rider(email: "user@example.com"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveUserAsync(TestUsers.Rider(email: "user@example.com")));

        Assert.Equal(2601, SqlErrorNumber(ex));
    }

    // I-086
    [Fact]
    public async Task AddAsync_大文字小文字違いのメール_DbUpdateExceptionで2601()
    {
        await SaveUserAsync(TestUsers.Rider(email: "user@example.com"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveUserAsync(TestUsers.Rider(email: "USER@example.com")));

        Assert.Equal(2601, SqlErrorNumber(ex));
    }

    // I-087
    [Fact]
    public async Task AddAsync_失敗後に同じコンテキストで別のユーザー_失敗したユーザーが残り再び失敗する_現状の挙動()
    {
        await SaveUserAsync(TestUsers.Rider(email: "user@example.com"));
        await using var db = CreateContext();
        var repository = new EfUserRepository(db);
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(TestUsers.Rider(email: "user@example.com")));

        // 失敗したユーザーが追跡に残っているので、別のメールでも同じ理由で失敗する（設計書 5 章 #10）
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(TestUsers.Rider(email: "other@example.com")));

        Assert.Equal(2601, SqlErrorNumber(ex));
        Assert.Equal(1, await CountAsync("users"));
    }

    // I-088
    [Fact]
    public async Task UpdateAsync_追跡中のユーザーに区分を追加_driversに行が追加される()
    {
        var user = await SaveUserAsync(TestUsers.Rider());
        await using (var db = CreateContext())
        {
            var repository = new EfUserRepository(db);
            var tracked = (await repository.FindByIdAsync(user.Id))!;
            tracked.AddRole(UserRole.Driver);
            await repository.UpdateAsync(tracked);
        }

        Assert.Equal(1, await CountAsync("drivers"));
        Assert.True((await FindByIdAsync(user.Id))!.HasRole(UserRole.Driver));
    }

    // I-089
    [Fact]
    public async Task UpdateAsync_追跡中のユーザーの区分を切り替え_active_roleがdriverになる()
    {
        var user = await SaveUserAsync(TestUsers.Both(UserRole.Rider));
        await using (var db = CreateContext())
        {
            var repository = new EfUserRepository(db);
            var tracked = (await repository.FindByIdAsync(user.Id))!;
            tracked.SwitchRole(UserRole.Driver);
            await repository.UpdateAsync(tracked);
        }

        await using (var db = CreateContext())
            Assert.Equal("driver", await db.Database.SqlQuery<string>($"SELECT active_role AS Value FROM users").SingleAsync());
        Assert.Equal(UserRole.Driver, (await FindByIdAsync(user.Id))!.ActiveRole);
    }

    // I-090
    [Fact]
    public async Task UpdateAsync_切り離されたユーザーの区分を切り替え_保存される()
    {
        var user = await SaveUserAsync(TestUsers.Both(UserRole.Rider));
        var detached = (await FindByIdAsync(user.Id))!; // コンテキスト A は破棄済み
        detached.SwitchRole(UserRole.Driver);

        await using (var db = CreateContext())
            await new EfUserRepository(db).UpdateAsync(detached);

        var read = (await FindByIdAsync(user.Id))!;
        Assert.Equal(UserRole.Driver, read.ActiveRole);
        Assert.True(read.HasRole(UserRole.Rider));
        Assert.True(read.HasRole(UserRole.Driver));
    }

    // I-091
    [Fact]
    public async Task UpdateAsync_切り離されたユーザーに区分を追加_DbUpdateConcurrencyException_現状の挙動()
    {
        var user = await SaveUserAsync(TestUsers.Rider());
        var detached = (await FindByIdAsync(user.Id))!;
        detached.AddRole(UserRole.Driver);

        // Update() が新しい Driver も更新扱いにし、UPDATE が 0 行になる（設計書 5 章 #9）
        await using (var db = CreateContext())
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new EfUserRepository(db).UpdateAsync(detached));

        Assert.Equal(0, await CountAsync("drivers"));
    }
}
