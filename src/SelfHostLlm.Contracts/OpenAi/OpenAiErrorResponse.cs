using System.Text.Json.Serialization;

namespace SelfHostLlm.Contracts.OpenAi;

/// <summary>
/// Format lỗi của OpenAI (<c>{ "error": { message, type, code } }</c>). Gateway trả lỗi theo
/// format này thay vì ProblemDetails để SDK client hiểu được.
/// </summary>
public sealed record OpenAiErrorResponse
{
    [JsonPropertyName("error")]
    public required OpenAiError Error { get; init; }

    public static OpenAiErrorResponse Create(string message, string type, string? code = null, string? param = null) =>
        new() { Error = new OpenAiError { Message = message, Type = type, Code = code, Param = param } };
}

public sealed record OpenAiError
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Ví dụ <c>invalid_request_error</c>, <c>authentication_error</c>, <c>rate_limit_error</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("param")]
    public string? Param { get; init; }
}
