using System.Text.Json;
using System.Text.Json.Serialization;

namespace SelfHostLlm.Contracts.OpenAi;

/// <summary>Body của <c>POST /v1/embeddings</c>. <see cref="Input"/> nhận cả string lẫn mảng string.</summary>
public sealed record EmbeddingsRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    [JsonConverter(typeof(StringOrArrayJsonConverter))]
    public required IReadOnlyList<string> Input { get; init; }

    [JsonPropertyName("encoding_format")]
    public string? EncodingFormat { get; init; }

    [JsonPropertyName("dimensions")]
    public int? Dimensions { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record EmbeddingsResponse
{
    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "list";

    [JsonPropertyName("data")]
    public required IReadOnlyList<EmbeddingData> Data { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("usage")]
    public EmbeddingsUsage? Usage { get; init; }
}

public sealed record EmbeddingData
{
    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "embedding";

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("embedding")]
    public required IReadOnlyList<float> Embedding { get; init; }
}

public sealed record EmbeddingsUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
