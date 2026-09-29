using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class ConsumerConfiguration : IEntityTypeConfiguration<Consumer>
{
    public void Configure(EntityTypeBuilder<Consumer> builder)
    {
        builder.ToTable("consumer");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasTenant();

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(1000);
    }
}
