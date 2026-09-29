using SelfHostLlm.Application.Common;
using SelfHostLlm.Domain.Usage;

namespace SelfHostLlm.Application.Abstractions;

public interface IUsageQueries
{
    Task<PagedResult<UsageRecord>> ListAsync(Guid tenantId, UsageRecordFilter filter, PageRequest page, CancellationToken cancellationToken);

    Task<UsageSummary> SummaryAsync(Guid tenantId, Guid? consumerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public sealed record UsageRecordFilter(DateTimeOffset? From, DateTimeOffset? To, Guid? ApiKeyId, Guid? DeploymentId);

/// <summary>Tổng hợp usage trong [From, To) — tách riêng số request có token ước lượng và số request phải fallback.</summary>
public sealed record UsageSummary(
    Guid? ConsumerId,
    DateTimeOffset From,
    DateTimeOffset To,
    long PromptTokens,
    long CompletionTokens,
    long RequestCount,
    long EstimatedRequestCount,
    long FallbackRequestCount)
{
    public long TotalTokens => PromptTokens + CompletionTokens;
}
