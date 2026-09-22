using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Rag;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("collection", t =>
        {
            // QĐ-1 ở tầng DB: chỉ chấp nhận embedding 1024 chiều.
            t.HasCheckConstraint("ck_collection_embedding_dim", $"embedding_dim = {EmbeddingConstants.Dimension}");
            t.HasCheckConstraint("ck_collection_chunk_overlap", "chunk_overlap >= 0 AND chunk_overlap < chunk_size");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasTenant();
        builder.HasTenantForeignKey<Collection, Model>(nameof(Collection.EmbeddingModelId));

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.EmbeddingDim).HasColumnName("embedding_dim");
        builder.Property(c => c.ChunkSize).HasColumnName("chunk_size");
        builder.Property(c => c.ChunkOverlap).HasColumnName("chunk_overlap");
    }
}
