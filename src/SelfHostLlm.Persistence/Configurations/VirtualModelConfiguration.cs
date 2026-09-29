using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class VirtualModelConfiguration : IEntityTypeConfiguration<VirtualModel>
{
    public void Configure(EntityTypeBuilder<VirtualModel> builder)
    {
        builder.ToTable("virtual_model");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.HasTenant();

        builder.Property(v => v.Name).HasMaxLength(64);
        builder.HasEnumColumn(v => v.Task, "task");
        builder.Property(v => v.Description).HasMaxLength(1000);

        // Gateway resolve virtual model theo (tenant của ApiKey, name).
        builder.HasIndex(v => new { v.TenantId, v.Name }).IsUnique();
    }
}
