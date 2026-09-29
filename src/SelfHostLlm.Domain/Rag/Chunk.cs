using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Rag;

/// <summary>
/// Một đoạn của <see cref="Document"/> kèm vector embedding. Mọi truy vấn vector trên chunk
/// BẮT BUỘC có <c>tenant_id</c> trong WHERE của chính câu truy vấn (bất biến số 3).
/// Nội dung chunk không bao giờ được log.
/// </summary>
public sealed class Chunk : Entity<Guid>, ITenantScoped
{
    private Chunk()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CollectionId { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>Thứ tự của chunk trong tài liệu, bắt đầu từ 0.</summary>
    public int Ordinal { get; private set; }

    public string Content { get; private set; } = null!;

    /// <summary>Luôn đúng <see cref="EmbeddingConstants.Dimension"/> phần tử. Persistence map sang pgvector.</summary>
    public ReadOnlyMemory<float> Embedding { get; private set; }

    public int TokenCount { get; private set; }

    public static Result<Chunk> Create(
        Guid tenantId,
        Guid collectionId,
        Guid documentId,
        int ordinal,
        string content,
        ReadOnlySpan<float> embedding,
        int tokenCount)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "chunk.tenant_id"),
            Guard.NotEmpty(collectionId, "chunk.collection_id"),
            Guard.NotEmpty(documentId, "chunk.document_id"),
            Guard.NonNegative(ordinal, "chunk.ordinal"),
            string.IsNullOrWhiteSpace(content) ? Error.Validation("chunk.content.empty", "Nội dung chunk rỗng.") : null,
            embedding.Length == EmbeddingConstants.Dimension
                ? null
                : Error.Validation(
                    "chunk.embedding.dimension_mismatch",
                    $"Embedding phải có đúng {EmbeddingConstants.Dimension} chiều."),
            Guard.NonNegative(tokenCount, "chunk.token_count"));
        if (error is not null)
        {
            return error;
        }

        return new Chunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CollectionId = collectionId,
            DocumentId = documentId,
            Ordinal = ordinal,
            Content = content,
            Embedding = embedding.ToArray(),
            TokenCount = tokenCount,
        };
    }
}
