using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace SelfHostLlm.Persistence;

/// <summary>Một nơi duy nhất cấu hình provider — dùng chung cho runtime, design-time và test.</summary>
public static class AppDbContextOptions
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        return builder.Build();
    }

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable);
            })
            .UseSnakeCaseNamingConvention();
    }
}

/// <summary>
/// Cho <c>dotnet ef</c> tạo context mà không cần khởi động host (không cần DB thật để sinh migration).
/// Connection string lấy từ biến môi trường <c>ConnectionStrings__Postgres</c> nếu có.
/// </summary>
internal sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=selfhostllm;Username=selfhostllm";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, AppDbContextOptions.BuildDataSource(connectionString));
        return new AppDbContext(options.Options);
    }
}
