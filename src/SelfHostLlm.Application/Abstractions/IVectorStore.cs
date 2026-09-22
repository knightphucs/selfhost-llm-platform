using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Rag;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Lưu và truy vấn vector chunk RAG. Mọi method bắt buộc nhận <c>tenantId</c> và implementation
/// phải đặt <c>tenant_id</c> trong WHERE của chính câu truy vấn vector — không bao giờ lọc
/// tenant sau khi đã lấy top-k (bất biến số 3).
/// </summary>
public interface IVectorStore
{
    /// <summary>Ghi chunk; trả <see cref="ErrorKind.Forbidden"/> nếu có chunk thuộc tenant khác.</summary>
    Task<Result> AddChunksAsync(Guid tenantId, IReadOnlyCollection<Chunk> chunks, CancellationToken cancellationToken);

    /// <summary>Top-k chunk gần nhất (cosine) trong một collection của tenant, gần nhất đứng đầu.</summary>
    Task<Result<IReadOnlyList<ChunkMatch>>> SearchAsync(
        Guid tenantId,
        Guid collectionId,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken);
}

/// <summary>Một kết quả truy vấn vector. <see cref="Distance"/> là cosine distance (0 = trùng hướng).</summary>
public sealed record ChunkMatch(Guid ChunkId, Guid DocumentId, int Ordinal, string Content, double Distance);
