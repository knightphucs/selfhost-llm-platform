namespace SelfHostLlm.Domain.Rag;

/// <summary>
/// QĐ-1: embedding cố định bge-m3, <c>vector(1024)</c>. pgvector cần dimension cố định mới
/// index HNSW được; đa-dimension là mở rộng tương lai (cần partition bảng).
/// </summary>
public static class EmbeddingConstants
{
    public const int Dimension = 1024;

    public const string DefaultModel = "bge-m3";
}
