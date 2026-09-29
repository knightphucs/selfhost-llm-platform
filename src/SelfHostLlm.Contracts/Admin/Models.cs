namespace SelfHostLlm.Contracts.Admin;

/// <param name="Capabilities">Giá trị: <c>Chat</c>, <c>Embedding</c>, <c>Vision</c>.</param>
public sealed record CreateModelRequest(
    string Name,
    string Family,
    string ParamSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TaskTags);

public sealed record UpdateModelRequest(
    string Name,
    string Family,
    string ParamSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TaskTags);

public sealed record ModelResponse(
    Guid Id,
    string Name,
    string Family,
    string ParamSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TaskTags,
    DateTimeOffset CreatedAt);
