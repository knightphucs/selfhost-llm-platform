using System.Text.Json.Serialization;

namespace SelfHostLlm.Contracts.OpenAi;

/// <summary>Response của <c>GET /v1/models</c> — gateway liệt kê các virtual model client được gọi.</summary>
public sealed record ModelListResponse
{
    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "list";

    [JsonPropertyName("data")]
    public required IReadOnlyList<ModelInfo> Data { get; init; }
}

public sealed record ModelInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "model";

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public required string OwnedBy { get; init; }
}
