using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Rag;

namespace SelfHostLlm.Persistence.Vector;

/// <summary>
/// <see cref="IVectorStore"/> trên pgvector. Truy vấn viết bằng SQL tường minh (có tham số) để
/// thấy rõ <c>tenant_id</c> nằm trong WHERE của chính câu truy vấn vector.
/// </summary>
internal sealed class PgVectorStore(AppDbContext db) : IVectorStore
{
    public const int MaxTopK = 100;

    public async Task<Result> AddChunksAsync(
        Guid tenantId,
        IReadOnlyCollection<Chunk> chunks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (tenantId == Guid.Empty)
        {
            return Error.Validation("vector_store.tenant_id.empty", "tenantId không được rỗng.");
        }

        if (chunks.Any(c => c.TenantId != tenantId))
        {
            return Error.Forbidden("vector_store.tenant_mismatch", "Có chunk không thuộc tenant đang thao tác.");
        }

        db.Chunks.AddRange(chunks);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ChunkMatch>>> SearchAsync(
        Guid tenantId,
        Guid collectionId,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var error = Validate(tenantId, collectionId, queryEmbedding, topK);
        if (error is not null)
        {
            return error;
        }

        var query = new Pgvector.Vector(queryEmbedding);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // pgvector ≥ 0.8: khi có điều kiện WHERE, HNSW quét tiếp cho tới khi đủ k dòng khớp
        // thay vì trả thiếu kết quả. SET LOCAL chỉ có hiệu lực trong transaction này.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = strict_order", cancellationToken);

        var rows = await db.Database.SqlQuery<ChunkMatchRow>($"""
            SELECT c.id          AS chunk_id,
                   c.document_id AS document_id,
                   c.ordinal     AS ordinal,
                   c.content     AS content,
                   c.embedding <=> {query} AS distance
            FROM chunk c
            WHERE c.tenant_id = {tenantId}
              AND c.collection_id = {collectionId}
            ORDER BY c.embedding <=> {query}
            LIMIT {topK}
            """).ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return rows.Select(r => new ChunkMatch(r.ChunkId, r.DocumentId, r.Ordinal, r.Content, r.Distance)).ToList();
    }

    private static Error? Validate(Guid tenantId, Guid collectionId, ReadOnlyMemory<float> queryEmbedding, int topK)
    {
        if (tenantId == Guid.Empty)
        {
            return Error.Validation("vector_store.tenant_id.empty", "tenantId không được rỗng.");
        }

        if (collectionId == Guid.Empty)
        {
            return Error.Validation("vector_store.collection_id.empty", "collectionId không được rỗng.");
        }

        if (queryEmbedding.Length != EmbeddingConstants.Dimension)
        {
            return Error.Validation(
                "vector_store.query.dimension_mismatch",
                $"Vector truy vấn phải có đúng {EmbeddingConstants.Dimension} chiều.");
        }

        return topK is < 1 or > MaxTopK
            ? Error.Validation("vector_store.top_k.out_of_range", $"topK phải trong khoảng 1–{MaxTopK}.")
            : null;
    }

    /// <summary>Kiểu hứng kết quả SQL thô (EF map theo tên cột snake_case).</summary>
    private sealed record ChunkMatchRow(Guid ChunkId, Guid DocumentId, int Ordinal, string Content, double Distance);
}
