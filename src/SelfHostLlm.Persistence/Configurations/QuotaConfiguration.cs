using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class QuotaConfiguration : IEntityTypeConfiguration<Quota>
{
    public void Configure(EntityTypeBuilder<Quota> builder)
    {
        builder.ToTable("quota");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.HasTenant(isPrincipal: false);
        builder.HasTenantForeignKey<Quota, Consumer>(nameof(Quota.ConsumerId));

        builder.HasIndex(q => q.ConsumerId).IsUnique();
    }
}
