namespace SelfHostLlm.Domain.Providers;

/// <summary>Loại backend suy luận; mỗi loại ứng với một adapter ở Adapters.Inference.</summary>
public enum ProviderKind
{
    Ollama,
    Vllm,
    Tgi,
    LlamaCpp,
    Mlx,

    /// <summary>Endpoint bất kỳ nói chuẩn OpenAI (LM Studio, server tự dựng...).</summary>
    OpenAiCompatible,
}
