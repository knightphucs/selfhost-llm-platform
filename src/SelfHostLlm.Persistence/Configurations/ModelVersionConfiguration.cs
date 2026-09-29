using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Training;
using SelfHostLlm.Persistence.Configurations.Conventions;
using SelfHostLlm.Persistence.Conversions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    public void Configure(EntityTypeBuilder<ModelVersion> builder)
    {
        builder.ToTable("model_version");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.HasTenant();
        builder.HasTenantForeignKey<ModelVersion, Model>(nameof(ModelVersion.ModelId));
        builder.HasTenantForeignKey<ModelVersion, TrainingJob>(nameof(ModelVersion.TrainingJobId));

        builder.Property(v => v.VersionTag).HasMaxLength(100);
        builder.Property(v => v.AdapterUri).HasMaxLength(1000);

        builder.Ignore(v => v.EvalMetrics);
        builder.Property<Dictionary<string, double>>("_evalMetrics")
            .HasColumnName("eval_metrics")
            .HasColumnType("jsonb")
            .HasConversion(ValueConversions.DictionaryToJson<double>(), ValueConversions.DictionaryComparer<double>());

        builder.HasIndex(v => new { v.ModelId, v.VersionTag }).IsUnique();
    }
}
