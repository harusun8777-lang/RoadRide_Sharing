using Backend.Tests.Infrastructure.Fixtures;
using Infrastructure;
using Infrastructure.Health;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Backend.Tests.Infrastructure;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Database")]
public class DatabaseHealthCheckTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Unavailable = "データベースに接続できません";

    private readonly RecordingLogger<DatabaseHealthCheck> _logger = new();

    private async Task<HealthCheckResult> CheckAsync(AppDbContext db, CancellationToken cancellationToken = default)
        => await new DatabaseHealthCheck(db, _logger).CheckHealthAsync(new HealthCheckContext(), cancellationToken);

    // コンテナのサーバーにある、存在しない DB の接続文字列
    private string MissingDatabaseConnectionString() => Fixture.CreateDatabaseConnectionString();

    // I-137
    [Fact]
    public async Task CheckHealthAsync_接続できる_Healthy()
    {
        await using var db = CreateContext();

        var result = await CheckAsync(db);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    // I-138
    [Fact]
    public async Task CheckHealthAsync_DBがない_Unhealthy()
    {
        await using var db = CreateContext(MissingDatabaseConnectionString());

        var result = await CheckAsync(db);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(Unavailable, result.Description);
    }

    // I-139
    [Fact]
    public async Task CheckHealthAsync_サーバーに届かない_例外にならずUnhealthy()
    {
        await using var db = CreateContext("Server=127.0.0.1,1;Database=unreachable;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True");

        var result = await CheckAsync(db);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(Unavailable, result.Description);
    }

    // I-140
    [Fact]
    public async Task CheckHealthAsync_Dispose済みのDbContext_詳細を含まないUnhealthy()
    {
        var db = CreateContext();
        await db.DisposeAsync();

        var result = await CheckAsync(db);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(Unavailable, result.Description);
        Assert.Null(result.Exception);
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        Assert.DoesNotContain(builder.DataSource, result.Description);
        Assert.DoesNotContain(builder.InitialCatalog, result.Description);
        Assert.DoesNotContain(builder.Password, result.Description);
    }

    // I-141
    [Fact]
    public async Task CheckHealthAsync_Dispose済みのDbContext_例外付きのErrorログが1件()
    {
        var db = CreateContext();
        await db.DisposeAsync();

        await CheckAsync(db);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<ObjectDisposedException>(entry.Exception);
    }

    // I-142
    [Fact]
    public async Task CheckHealthAsync_DBがない_ログを出さない_現状の挙動()
    {
        await using var db = CreateContext(MissingDatabaseConnectionString());

        await CheckAsync(db);

        Assert.Empty(_logger.Entries);
    }

    // I-143
    [Fact]
    public async Task CheckHealthAsync_キャンセル済みのトークン_例外が伝わらずUnhealthy_現状の挙動()
    {
        await using var db = CreateContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // catch (Exception) がキャンセルも受けて Unhealthy に変える（設計書 5 章 #16）
        var result = await CheckAsync(db, cts.Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(Unavailable, result.Description);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsAssignableFrom<OperationCanceledException>(entry.Exception);
    }
}
