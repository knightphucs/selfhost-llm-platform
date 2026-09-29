using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("usage_record");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).UseIdentityAlwaysColumn();
        builder.HasTenant(isPrincipal: false);
        builder.HasTenantForeignKey<UsageRecord, ApiKey>(nameof(UsageRecord.ApiKeyId));
        builder.HasTenantForeignKey<UsageRecord, Deployment>(nameof(UsageRecord.DeploymentId));

        builder.Property(u => u.RequestedModel).HasMaxLength(200);
        builder.HasEnumColumn<UsageRecord, TaskKind>(u => u.Task, "task");
        builder.Ignore(u => u.Tokens);

        builder.HasIndex(u => new { u.TenantId, u.OccurredAt });
    }
}
