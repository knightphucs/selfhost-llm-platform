using Microsoft.EntityFrameworkCore;
using Npgsql;
using SelfHostLlm.Persistence;
using Testcontainers.PostgreSql;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;

/// <summary>
/// Một container Postgres + pgvector dùng chung cho cả test run. Migration được apply lên
/// container dùng-xong-bỏ này — không bao giờ chạm DB dev.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    private NpgsqlDataSource? _dataSource;
    private ControlPlaneApp? _controlPlane;

    public string ConnectionString => _container.GetConnectionString();

    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("Fixture chưa khởi tạo.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = await MigrateAsync(_container.GetConnectionString());
    }

    /// <summary>Control plane chạy thật (WebApplicationFactory) trên DB dùng chung — tạo lười một lần.</summary>
    public async Task<ControlPlaneApp> GetControlPlaneAsync() =>
        _controlPlane ??= await ControlPlaneApp.StartAsync(ConnectionString, seedPlatformAdmin: true);

    /// <summary>Tạo một database mới tinh (đã migrate) trong cùng container — cho test cần DB trống.</summary>
    public async Task<string> CreateFreshDatabaseAsync()
    {
        var name = "fresh_" + Guid.NewGuid().ToString("N")[..12];
        await using (var command = DataSource.CreateCommand($"CREATE DATABASE {name}"))
        {
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
        await (await MigrateAsync(connectionString)).DisposeAsync();
        return connectionString;
    }

    private static async Task<NpgsqlDataSource> MigrateAsync(string connectionString)
    {
        var dataSource = AppDbContextOptions.BuildDataSource(connectionString);
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, dataSource);
        await using (var db = new AppDbContext(options.Options))
        {
            await db.Database.MigrateAsync();
        }

        // Extension vector vừa được migration tạo → nạp lại type để Npgsql nhận kiểu vector.
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ReloadTypesAsync();
        return dataSource;
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, DataSource);
        return new AppDbContext(options.Options);
    }

    public async Task DisposeAsync()
    {
        if (_controlPlane is not null)
        {
            await _controlPlane.DisposeAsync();
        }

        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresDatabase : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
