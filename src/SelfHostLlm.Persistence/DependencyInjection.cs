using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Persistence.Vector;

namespace SelfHostLlm.Persistence;

public static class DependencyInjection
{
    /// <summary>
    /// Đăng ký <see cref="AppDbContext"/> trên Npgsql data source có bật pgvector.
    /// Đọc <c>ConnectionStrings:Postgres</c>. Không tự chạy migration.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:Postgres.");

        services.AddSingleton(_ => AppDbContextOptions.BuildDataSource(connectionString));
        services.AddDbContext<AppDbContext>((sp, options) =>
            AppDbContextOptions.Configure(options, sp.GetRequiredService<NpgsqlDataSource>()));

        services.AddScoped<IVectorStore, PgVectorStore>();

        return services;
    }
}
