using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Persistence.Configurations.Conventions;
using SelfHostLlm.Persistence.Conversions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> builder)
    {
        builder.ToTable("model");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasTenant();

        builder.Property(m => m.Name).HasMaxLength(200);
        builder.Property(m => m.Family).HasMaxLength(100);
        builder.Property(m => m.ParamSize).HasMaxLength(20);
        builder.Property(m => m.Quantization).HasMaxLength(50);

        builder.Ignore(m => m.Capabilities);
        builder.Property<List<ModelCapability>>("_capabilities")
            .HasColumnName("capabilities")
            .HasConversion(ValueConversions.EnumListToStringArray<ModelCapability>(), ValueConversions.ListComparer<ModelCapability>());
        builder.HasEnumArrayCheck<Model, ModelCapability>("capabilities");

        builder.Ignore(m => m.TaskTags);
        builder.Property<List<string>>("_taskTags").HasColumnName("task_tags");

        // Lệch ERD có chủ đích: tên model duy nhất trong phạm vi tenant, không phải toàn cục.
        builder.HasIndex(m => new { m.TenantId, m.Name }).IsUnique();
    }
}
