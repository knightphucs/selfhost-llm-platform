namespace SelfHostLlm.Application.Routing;

/// <summary>Kết quả do delegate <c>attempt</c> trả về cho <see cref="FallbackExecutor"/>.</summary>
public sealed record AttemptResult<T>
{
    private AttemptResult(AttemptOutcome outcome, T? value, int? statusCode, string? reason)
    {
        Outcome = outcome;
        Value = value;
        StatusCode = statusCode;
        Reason = reason;
    }

    public AttemptOutcome Outcome { get; }

    public T? Value { get; }

    public int? StatusCode { get; }

    public string? Reason { get; }

    public static AttemptResult<T> Success(T value, int? statusCode = null) =>
        new(AttemptOutcome.Success, value, statusCode, null);

    /// <summary>4xx của client: giữ response để relay nguyên văn, không thử tiếp.</summary>
    public static AttemptResult<T> ClientError(T value, int statusCode) =>
        new(AttemptOutcome.ClientError, value, statusCode, $"HTTP {statusCode}");

    /// <summary>Lỗi tạm thời. Caller tự giải phóng response (nếu có) trước khi trả kết quả này.</summary>
    public static AttemptResult<T> Transient(int? statusCode, string reason) =>
        new(AttemptOutcome.Transient, default, statusCode, reason);

    /// <summary>Phân loại theo status bằng <see cref="UpstreamStatus.Classify"/>.</summary>
    public static AttemptResult<T> FromStatus(int statusCode, T value) => UpstreamStatus.Classify(statusCode) switch
    {
        AttemptOutcome.Success => Success(value, statusCode),
        AttemptOutcome.ClientError => ClientError(value, statusCode),
        _ => Transient(statusCode, $"HTTP {statusCode}"),
    };
}

/// <summary>Vết một lần thử — dùng cho log và cột <c>used_fallback</c>.</summary>
public sealed record AttemptRecord(Guid DeploymentId, AttemptOutcome Outcome, int? StatusCode, string? Reason, TimeSpan Elapsed);

/// <summary>Kết quả sau khi đi hết (hoặc dừng sớm trên) chuỗi fallback.</summary>
public sealed record FallbackResult<T>
{
    public T? Value { get; init; }

    /// <summary>Deployment đã trả response cho client; null khi mọi lần thử đều lỗi.</summary>
    public DeploymentEntry? ServedBy { get; init; }

    public required IReadOnlyList<AttemptRecord> Attempts { get; init; }

    /// <summary>Khác null khi hết ứng viên mà không có response nào relay được.</summary>
    public Domain.Common.Error? Error { get; init; }

    public bool IsSuccess => Error is null;

    /// <summary>Response đến từ 4xx của client (được relay, không fallback).</summary>
    public bool IsClientError => Attempts.Count > 0 && Attempts[^1].Outcome == AttemptOutcome.ClientError;

    /// <summary>Đã phải thử quá một deployment — giá trị cho cột <c>usage_record.used_fallback</c>.</summary>
    public bool UsedFallback => Attempts.Count > 1;
}
