using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Rag;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Domain.Tenancy;
using SelfHostLlm.Persistence;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;

/// <summary>Tạo dữ liệu mẫu qua factory của Domain. Mỗi test tự tạo tenant riêng để không giẫm nhau.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public static Tenant Tenant() => SelfHostLlm.Domain.Tenancy.Tenant.Create("Tenant", $"t-{Guid.NewGuid():N}", Now).Value;

    public static Model ChatModel(Guid tenantId) => Model.Create(
        tenantId, $"Qwen2.5-7B-{Guid.NewGuid():N}", "qwen2.5", "7B", "Q4_K_M", 32768,
        [ModelCapability.Chat], ["coding"], Now).Value;

    public static Model EmbeddingModel(Guid tenantId) => Model.Create(
        tenantId, $"bge-m3-{Guid.NewGuid():N}", "bge", "568M", "FP16", 8192,
        [ModelCapability.Embedding], [], Now).Value;

    public static Provider Provider(Guid tenantId) =>
        SelfHostLlm.Domain.Providers.Provider.Create(tenantId, "Ollama PC", ProviderKind.Ollama, null).Value;

    public static Deployment Deployment(Guid tenantId, Guid modelId, Guid providerId) =>
        SelfHostLlm.Domain.Deployments.Deployment.Create(
            tenantId, modelId, null, providerId,
            Address.Create($"http://192.168.1.50:{Random.Shared.Next(1024, 65000)}").Value,
            "qwen2.5:7b-instruct-q4_K_M", null).Value;

    public static VirtualModel VirtualModel(Guid tenantId) =>
        SelfHostLlm.Domain.Routing.VirtualModel.Create(tenantId, $"code-{Guid.NewGuid():N}"[..20], TaskKind.Coding, null).Value;

    public static Consumer Consumer(Guid tenantId) =>
        SelfHostLlm.Domain.Access.Consumer.Create(tenantId, "App nội bộ", null).Value;

    public static ApiKey ApiKey(Guid tenantId, Guid consumerId, string? hash = null) =>
        SelfHostLlm.Domain.Access.ApiKey.Create(
            tenantId, consumerId, hash ?? RandomHash(), "sk-test", null, Now).Value;

    public static Collection Collection(Guid tenantId, Guid embeddingModelId) =>
        SelfHostLlm.Domain.Rag.Collection.Create(
            tenantId, "Tài liệu nội bộ", embeddingModelId, EmbeddingConstants.Dimension, 512, 64, Now).Value;

    public static Document Document(Guid tenantId, Guid collectionId) =>
        SelfHostLlm.Domain.Rag.Document.Create(
            tenantId, collectionId, "Sổ tay", null, RandomHash(), "text/markdown", Now).Value;

    public static Chunk Chunk(Guid tenantId, Guid collectionId, Guid documentId, int ordinal, float[] embedding) =>
        SelfHostLlm.Domain.Rag.Chunk.Create(tenantId, collectionId, documentId, ordinal, $"đoạn {ordinal}", embedding, 10).Value;

    /// <summary>Vector đơn vị e_i (1024 chiều) — cosine distance giữa hai e_i khác nhau bằng 1.</summary>
    public static float[] UnitVector(int index)
    {
        var vector = new float[EmbeddingConstants.Dimension];
        vector[index] = 1f;
        return vector;
    }

    public static string RandomHash() => Convert.ToHexString(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()).ToLowerInvariant();

    /// <summary>Lưu một tenant kèm các entity cho trước.</summary>
    public static async Task SaveAsync(AppDbContext db, params object[] entities)
    {
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }
}
