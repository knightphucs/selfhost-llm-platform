using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Rag;
using SelfHostLlm.Persistence.Configurations.Conventions;
using SelfHostLlm.Persistence.Conversions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> builder)
    {
        builder.ToTable("chunk");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasTenant(isPrincipal: false);
        builder.HasTenantForeignKey<Chunk, Collection>(nameof(Chunk.CollectionId), DeleteBehavior.Cascade);
        builder.HasTenantForeignKey<Chunk, Document>(nameof(Chunk.DocumentId), DeleteBehavior.Cascade);

        builder.Property(c => c.Embedding)
            .HasColumnType($"vector({EmbeddingConstants.Dimension})")
            .HasConversion(ValueConversions.MemoryToVector, ValueConversions.MemoryComparer);

        // Index vector HNSW (vector_cosine_ops) viết bằng SQL trong migration.
        builder.HasIndex(c => new { c.TenantId, c.CollectionId });
        builder.HasIndex(c => new { c.DocumentId, c.Ordinal }).IsUnique();
    }
}
