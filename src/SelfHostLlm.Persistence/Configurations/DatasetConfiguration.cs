using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Training;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.ToTable("dataset");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.HasTenant();

        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.StorageUri).HasMaxLength(1000);
        builder.Property(d => d.Format).HasMaxLength(50);
    }
}
