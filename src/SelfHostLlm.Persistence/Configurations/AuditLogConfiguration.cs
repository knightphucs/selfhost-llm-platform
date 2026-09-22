using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Persistence.Configurations.Conventions;
using SelfHostLlm.Persistence.Identity;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();
        builder.HasTenant(isPrincipal: false);

        // FK đơn: PlatformAdmin thao tác xuyên tenant nên actor không nhất thiết cùng tenant với bản ghi.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .HasConstraintName("fk_audit_log_actor_user_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasEnumColumn(a => a.Action, "action");
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.BeforeValue).HasColumnType("jsonb");
        builder.Property(a => a.AfterValue).HasColumnType("jsonb");
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        // Append-only được ép bằng trigger trong migration InitialSchema.
        builder.HasIndex(a => new { a.TenantId, a.OccurredAt });
    }
}
