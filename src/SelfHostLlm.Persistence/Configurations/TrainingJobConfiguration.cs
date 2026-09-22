using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Training;
using SelfHostLlm.Persistence.Configurations.Conventions;
using SelfHostLlm.Persistence.Conversions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class TrainingJobConfiguration : IEntityTypeConfiguration<TrainingJob>
{
    public void Configure(EntityTypeBuilder<TrainingJob> builder)
    {
        builder.ToTable("training_job");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.HasTenant();
        builder.HasTenantForeignKey<TrainingJob, Model>(nameof(TrainingJob.BaseModelId));
        builder.HasTenantForeignKey<TrainingJob, Dataset>(nameof(TrainingJob.DatasetId));

        builder.HasEnumColumn(j => j.Method, "method");
        builder.HasEnumColumn(j => j.Status, "status");
        builder.Property(j => j.LogUri).HasMaxLength(1000);
        builder.Ignore(j => j.IsTerminal);

        builder.Ignore(j => j.Hyperparameters);
        builder.Property<Dictionary<string, string>>("_hyperparameters")
            .HasColumnName("hyperparameters")
            .HasColumnType("jsonb")
            .HasConversion(ValueConversions.DictionaryToJson<string>(), ValueConversions.DictionaryComparer<string>());
    }
}
