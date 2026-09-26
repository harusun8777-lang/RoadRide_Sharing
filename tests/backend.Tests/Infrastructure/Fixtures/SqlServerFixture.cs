using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Backend.Tests.Infrastructure.Fixtures;

// テスト実行全体で共有する SQL Server 2022 のコンテナ
public sealed class SqlServerFixture : IAsyncLifetime
{
    // docker-compose.yml と同じイメージ
    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // test_{Guid:N} という新しい DB 名の接続文字列を返す（DB はまだ作らない）
    public string CreateDatabaseConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"test_{Guid.NewGuid():N}",
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
