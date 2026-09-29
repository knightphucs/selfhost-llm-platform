using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Persistence.Configurations.Conventions;
using SelfHostLlm.Persistence.Conversions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("deployment");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.HasTenant();
        builder.HasTenantForeignKey<Deployment, Model>(nameof(Deployment.ModelId));
        builder.HasTenantForeignKey<Deployment, ModelVersion>(nameof(Deployment.ModelVersionId));
        builder.HasTenantForeignKey<Deployment, Provider>(nameof(Deployment.ProviderId));

        // Address trong domain = cột base_url trong ERD.
        builder.Property(d => d.Address)
            .HasColumnName("base_url")
            .HasMaxLength(500)
            .HasConversion(ValueConversions.AddressToString);
        builder.Property(d => d.RemoteModelName).HasMaxLength(200);
        builder.HasEnumColumn(d => d.HealthStatus, "health_status");
        builder.Ignore(d => d.IsRoutable);

        builder.HasIndex(d => new { d.ProviderId, d.Address, d.RemoteModelName }).IsUnique();
    }
}
