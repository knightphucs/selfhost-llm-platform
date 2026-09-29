using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_key");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.HasTenant();
        builder.HasTenantForeignKey<ApiKey, Consumer>(nameof(ApiKey.ConsumerId));

        builder.Property(k => k.KeyHash).HasMaxLength(ApiKey.KeyHashLength).IsFixedLength();
        builder.Property(k => k.KeyPrefix).HasMaxLength(ApiKey.MaxKeyPrefixLength);
        builder.Ignore(k => k.IsRevoked);

        builder.HasIndex(k => k.KeyHash).IsUnique();
    }
}
