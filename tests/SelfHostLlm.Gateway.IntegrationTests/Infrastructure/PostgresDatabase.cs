using Microsoft.EntityFrameworkCore;
using Npgsql;
using SelfHostLlm.Persistence;
using Testcontainers.PostgreSql;

namespace SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

/// <summary>Postgres + pgvector (Testcontainers) đã migrate — dùng cho test ghi usage và E2E.</summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    public const string CollectionName = "gateway-postgres";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var dataSource = AppDbContextOptions.BuildDataSource(ConnectionString);
        await using (var db = CreateDbContext(dataSource))
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ReloadTypesAsync();
    }

    public static AppDbContext CreateDbContext(NpgsqlDataSource dataSource)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, dataSource);
        return new AppDbContext(options.Options);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(PostgresDatabase.CollectionName)]
public sealed class PostgresDatabaseGroup : ICollectionFixture<PostgresDatabase>
{
}
