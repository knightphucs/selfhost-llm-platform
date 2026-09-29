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

    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("Fixture chưa khởi tạo.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = AppDbContextOptions.BuildDataSource(_container.GetConnectionString());

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        // Extension vector vừa được migration tạo → nạp lại type để Npgsql nhận kiểu vector.
        await using var connection = await DataSource.OpenConnectionAsync();
        await connection.ReloadTypesAsync();
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, DataSource);
        return new AppDbContext(options.Options);
    }

    public async Task DisposeAsync()
    {
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
