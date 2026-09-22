using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Domain.Usage;

/// <summary>
/// Bản ghi một request suy luận. Append-only: gateway ghi async qua batch (QĐ-2),
/// không có thao tác sửa. <c>Id</c> do database sinh.
/// </summary>
public sealed class UsageRecord : Entity<long>, ITenantScoped
{
    private UsageRecord()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ApiKeyId { get; private set; }

    /// <summary>Deployment đã phục vụ; null khi mọi deployment trong chuỗi fallback đều lỗi.</summary>
    public Guid? DeploymentId { get; private set; }

    /// <summary>Tên model client gửi lên (thường là virtual model).</summary>
    public string RequestedModel { get; private set; } = null!;

    public TaskKind? Task { get; private set; }

    public int PromptTokens { get; private set; }

    public int CompletionTokens { get; private set; }

    public bool TokensEstimated { get; private set; }

    public int LatencyMs { get; private set; }

    public int StatusCode { get; private set; }

    public bool UsedFallback { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public TokenCount Tokens => TokenCount.Create(PromptTokens, CompletionTokens, TokensEstimated).Value;

    public static Result<UsageRecord> Create(
        Guid tenantId,
        Guid apiKeyId,
        Guid? deploymentId,
        string requestedModel,
        TaskKind? task,
        TokenCount tokens,
        int latencyMs,
        int statusCode,
        bool usedFallback,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var error = Guard.First(
            Guard.NotEmpty(tenantId, "usage.tenant_id"),
            Guard.NotEmpty(apiKeyId, "usage.api_key_id"),
            Guard.NotBlank(requestedModel, "usage.requested_model"),
            Guard.NonNegative(latencyMs, "usage.latency_ms"),
            statusCode is >= 100 and <= 599
                ? null
                : Error.Validation("usage.status_code.invalid", "status_code phải trong khoảng 100–599."));
        if (error is not null)
        {
            return error;
        }

        return new UsageRecord
        {
            TenantId = tenantId,
            ApiKeyId = apiKeyId,
            DeploymentId = deploymentId,
            RequestedModel = requestedModel,
            Task = task,
            PromptTokens = tokens.Prompt,
            CompletionTokens = tokens.Completion,
            TokensEstimated = tokens.IsEstimated,
            LatencyMs = latencyMs,
            StatusCode = statusCode,
            UsedFallback = usedFallback,
            OccurredAt = occurredAt.ToUniversalTime(),
        };
    }
}
