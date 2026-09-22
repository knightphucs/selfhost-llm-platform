using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Rag;
using SelfHostLlm.Persistence.Configurations.Conventions;

namespace SelfHostLlm.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("document");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.HasTenant();
        builder.HasTenantForeignKey<Document, Collection>(nameof(Document.CollectionId), DeleteBehavior.Cascade);

        builder.Property(d => d.Title).HasMaxLength(500);
        builder.Property(d => d.SourceUri).HasMaxLength(2000);
        builder.Property(d => d.ContentHash).HasMaxLength(Document.ContentHashLength).IsFixedLength();
        builder.Property(d => d.MimeType).HasMaxLength(100);

        // Chống ingest trùng cùng một tài liệu trong một collection.
        builder.HasIndex(d => new { d.TenantId, d.CollectionId, d.ContentHash }).IsUnique();
    }
}
