using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class UsageAggregateConfiguration : IEntityTypeConfiguration<UsageAggregate>
{
    public void Configure(EntityTypeBuilder<UsageAggregate> builder)
    {
        builder.ToTable("usage_aggregate");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();
        builder.HasTenant(isPrincipal: false);
        builder.HasTenantForeignKey<UsageAggregate, Consumer>(nameof(UsageAggregate.ConsumerId));

        builder.HasEnumColumn(a => a.Period, "period");

        builder.HasIndex(a => new { a.TenantId, a.ConsumerId, a.Period, a.BucketStart }).IsUnique();
    }
}
