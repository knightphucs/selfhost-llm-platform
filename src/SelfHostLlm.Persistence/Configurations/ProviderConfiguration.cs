using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("provider");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasTenant();

        builder.Property(p => p.Name).HasMaxLength(200);
        builder.HasEnumColumn(p => p.Kind, "kind");
        builder.Property(p => p.Description).HasMaxLength(1000);
    }
}
