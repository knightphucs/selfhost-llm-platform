using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Persistence.Identity;

namespace SelfHostLlm.Persistence.Configurations.Identity;

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("role");
        builder.HasIndex(r => r.NormalizedName).HasDatabaseName("ix_role_normalized_name");
        builder.Ignore(r => r.Permissions);
        builder.Property<List<string>>("_permissions").HasColumnName("permissions");
        builder.HasData(AppRoleSeed.Rows());
    }
}
