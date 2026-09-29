using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("route");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasTenant(isPrincipal: false);
        builder.HasTenantForeignKey<Route, VirtualModel>(nameof(Route.VirtualModelId));
        builder.HasTenantForeignKey<Route, Deployment>(nameof(Route.DeploymentId));

        builder.HasIndex(r => new { r.VirtualModelId, r.Priority });
        builder.HasIndex(r => new { r.VirtualModelId, r.DeploymentId }).IsUnique();
    }
}
