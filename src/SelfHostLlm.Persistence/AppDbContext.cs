using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Rag;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Domain.Tenancy;
using SelfHostLlm.Domain.Training;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Persistence.Identity;

namespace SelfHostLlm.Persistence;

/// <summary>
/// DbContext duy nhất của nền tảng. Kế thừa Identity để quản lý user/role quản trị;
/// mọi entity nghiệp vụ cấu hình trong <c>Configurations/</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<
        AppUser,
        AppRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Model> Models => Set<Model>();

    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();

    public DbSet<Provider> Providers => Set<Provider>();

    public DbSet<Deployment> Deployments => Set<Deployment>();

    public DbSet<VirtualModel> VirtualModels => Set<VirtualModel>();

    public DbSet<Route> Routes => Set<Route>();

    public DbSet<Consumer> Consumers => Set<Consumer>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<Quota> Quotas => Set<Quota>();

    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    public DbSet<UsageAggregate> UsageAggregates => Set<UsageAggregate>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Collection> Collections => Set<Collection>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<Chunk> Chunks => Set<Chunk>();

    public DbSet<Dataset> Datasets => Set<Dataset>();

    public DbSet<TrainingJob> TrainingJobs => Set<TrainingJob>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
