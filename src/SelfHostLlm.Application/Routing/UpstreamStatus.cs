namespace SelfHostLlm.Application.Routing;

/// <summary>Kết quả của một lần gọi tới một deployment.</summary>
public enum AttemptOutcome
{
    /// <summary>2xx/3xx — relay cho client.</summary>
    Success,

    /// <summary>4xx do client (trừ 408/429) — relay nguyên cho client, KHÔNG fallback.</summary>
    ClientError,

    /// <summary>Lỗi kết nối, timeout, 5xx, 408, 429 — thử deployment kế tiếp.</summary>
    Transient,
}

public static class UpstreamStatus
{
    /// <summary>
    /// Phân loại HTTP status từ engine. 408 và 429 là engine bận chứ không phải lỗi của client
    /// nên được fallback như 5xx; các 4xx còn lại trả thẳng về client.
    /// </summary>
    public static AttemptOutcome Classify(int statusCode) => statusCode switch
    {
        408 or 429 => AttemptOutcome.Transient,
        >= 500 => AttemptOutcome.Transient,
        >= 400 => AttemptOutcome.ClientError,
        _ => AttemptOutcome.Success,
    };
}

/// <summary>
/// Adapter bọc lỗi hạ tầng không phải <see cref="HttpRequestException"/> (ví dụ circuit breaker
/// đang mở) vào exception này để <see cref="FallbackExecutor"/> coi là transient.
/// </summary>
public sealed class UpstreamUnavailableException : Exception
{
    public UpstreamUnavailableException()
    {
    }

    public UpstreamUnavailableException(string message)
        : base(message)
    {
    }

    public UpstreamUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
