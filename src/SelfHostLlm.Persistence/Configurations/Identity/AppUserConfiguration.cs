using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Tenancy;
using SelfHostLlm.Persistence.Identity;

namespace SelfHostLlm.Persistence.Configurations.Identity;

/// <summary>Bảng Identity đổi tên về tên trong ERD: <c>app_user</c>, cột <c>username</c>.</summary>
internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.Property(u => u.UserName).HasColumnName("username");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .HasConstraintName("fk_app_user_tenant")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.TenantId);

        // Index mặc định của Identity đổi tên về snake_case cho đồng nhất.
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_app_user_normalized_user_name");
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_app_user_normalized_email");
    }
}
