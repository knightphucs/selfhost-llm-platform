namespace SelfHostLlm.Contracts.Admin;

public sealed record UsageRecordResponse(
    long Id,
    Guid ApiKeyId,
    Guid? DeploymentId,
    string RequestedModel,
    string? Task,
    int PromptTokens,
    int CompletionTokens,
    bool TokensEstimated,
    int LatencyMs,
    int StatusCode,
    bool UsedFallback,
    DateTimeOffset OccurredAt);

/// <summary>
/// Tổng hợp usage trong khoảng [From, To). Tách riêng số request có token ước lượng và số
/// request phải fallback — hai con số báo cáo cần.
/// </summary>
public sealed record UsageSummaryResponse(
    Guid? ConsumerId,
    DateTimeOffset From,
    DateTimeOffset To,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    long RequestCount,
    long EstimatedRequestCount,
    long FallbackRequestCount);
