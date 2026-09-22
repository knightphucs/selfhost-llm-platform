using System.Diagnostics.CodeAnalysis;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Rag;

/// <summary>
/// Tập tài liệu RAG của một tenant. <see cref="EmbeddingDim"/> là guard: từ chối mọi collection
/// có dimension khác <see cref="EmbeddingConstants.Dimension"/> (QĐ-1).
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "Tên entity phải khớp ERD (COLLECTION).")]
public sealed class Collection : Entity<Guid>, ITenantScoped
{
    private Collection()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public Guid EmbeddingModelId { get; private set; }

    public int EmbeddingDim { get; private set; }

    public int ChunkSize { get; private set; }

    public int ChunkOverlap { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Collection> Create(
        Guid tenantId,
        string name,
        Guid embeddingModelId,
        int embeddingDim,
        int chunkSize,
        int chunkOverlap,
        DateTimeOffset now)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "collection.tenant_id"),
            Guard.NotBlank(name, "collection.name"),
            Guard.NotEmpty(embeddingModelId, "collection.embedding_model_id"),
            embeddingDim == EmbeddingConstants.Dimension
                ? null
                : Error.Validation(
                    "collection.embedding_dim.unsupported",
                    $"Chỉ hỗ trợ embedding {EmbeddingConstants.Dimension} chiều."),
            Guard.Positive(chunkSize, "collection.chunk_size"),
            Guard.NonNegative(chunkOverlap, "collection.chunk_overlap"),
            chunkOverlap < chunkSize
                ? null
                : Error.Validation("collection.chunk_overlap.too_large", "chunk_overlap phải nhỏ hơn chunk_size."));
        if (error is not null)
        {
            return error;
        }

        return new Collection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            EmbeddingModelId = embeddingModelId,
            EmbeddingDim = embeddingDim,
            ChunkSize = chunkSize,
            ChunkOverlap = chunkOverlap,
            CreatedAt = now.ToUniversalTime(),
        };
    }
}
