namespace SelfHostLlm.Contracts.Admin;

/// <param name="Kind">Giá trị: <c>Ollama</c>, <c>Vllm</c>, <c>Tgi</c>, <c>LlamaCpp</c>, <c>Mlx</c>, <c>OpenAiCompatible</c>.</param>
public sealed record CreateProviderRequest(string Name, string Kind, string? Description);

public sealed record UpdateProviderRequest(string Name, string Kind, string? Description);

public sealed record ProviderResponse(Guid Id, string Name, string Kind, string? Description);
