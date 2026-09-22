using System.Text.Json;
using System.Text.Json.Serialization;

namespace SelfHostLlm.Contracts.OpenAi;

/// <summary>
/// Body của <c>POST /v1/chat/completions</c>. Gateway chỉ đọc các field cần cho routing và
/// metering; mọi field khác được giữ nguyên trong <see cref="ExtensionData"/> để forward.
/// </summary>
public sealed record ChatCompletionRequest
{
    /// <summary>Virtual model (<c>code-fast</c>...) hoặc tên model thật.</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; init; }

    /// <summary>Gateway tự bật <c>include_usage</c> khi stream (QĐ-4).</summary>
    [JsonPropertyName("stream_options")]
    public StreamOptions? StreamOptions { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("stop")]
    [JsonConverter(typeof(StringOrArrayJsonConverter))]
    public IReadOnlyList<string>? Stop { get; init; }

    /// <summary>Field mở rộng của nền tảng: chỉ định đầu việc cho explicit task routing.</summary>
    [JsonPropertyName("task")]
    public string? Task { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record StreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; init; }
}

/// <summary>
/// Một message. <see cref="Content"/> giữ dạng <see cref="JsonElement"/> vì có thể là string
/// hoặc mảng part (vision). Nội dung prompt không bao giờ được log.
/// </summary>
public sealed record ChatMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public JsonElement Content { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    public static ChatMessage Text(string role, string text) => new()
    {
        Role = role,
        Content = JsonSerializer.SerializeToElement(text),
    };
}
