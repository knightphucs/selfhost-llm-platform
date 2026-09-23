using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Persistence.Configuration;
using SelfHostLlm.Persistence.Identity;
using SelfHostLlm.Persistence.Repositories;
using SelfHostLlm.Persistence.Security;
using SelfHostLlm.Persistence.VectorStore;

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

        services.AddScoped(typeof(ITenantRepository<>), typeof(TenantRepository<>));
        services.AddScoped<ITenantStore, TenantStore>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IUsageQueries, UsageQueries>();
        services.AddScoped<IAuditLogQueries, AuditLogQueries>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVectorStore, PgVectorStore>();
        services.AddScoped<IConfigSnapshotSource, ConfigSnapshotSource>();

        // Cần UserManager — chỉ resolve được ở host đã đăng ký Identity (ControlPlane).
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }

    /// <summary>
    /// Data Protection cho secret lưu trong DB (QĐ-8). Key ring nằm ở thư mục file NGOÀI DB —
    /// có bản dump DB cũng không giải mã được. ControlPlane và Gateway phải trỏ cùng thư mục và
    /// cùng ApplicationName để giải mã chung. Mặc định: thư mục dữ liệu ứng dụng của user hiện
    /// tại (ngoài repo). Giới hạn: trên macOS/Linux key file chỉ được bảo vệ bằng quyền file.
    /// </summary>
    public static IServiceCollection AddSecretProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var keysPath = configuration["DataProtection:KeysPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SelfHostLlm", "dp-keys");

        services.AddDataProtection()
            .SetApplicationName("SelfHostLlm")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        return services;
    }
}
