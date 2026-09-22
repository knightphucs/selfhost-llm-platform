using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Access;

/// <summary>
/// Giới hạn sử dụng của một <see cref="Consumer"/>. Mỗi giới hạn là tuỳ chọn:
/// <c>null</c> nghĩa là không giới hạn chiều đó.
/// </summary>
public sealed class Quota : Entity<Guid>, ITenantScoped
{
    private Quota()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ConsumerId { get; private set; }

    /// <summary>Rate limit: token/phút.</summary>
    public int? TokensPerMinute { get; private set; }

    /// <summary>Budget: token/tháng.</summary>
    public long? TokensPerMonth { get; private set; }

    public int? MaxConcurrentRequests { get; private set; }

    public static Result<Quota> Create(
        Guid tenantId,
        Guid consumerId,
        int? tokensPerMinute,
        long? tokensPerMonth,
        int? maxConcurrentRequests)
    {
        var quota = new Quota { Id = Guid.NewGuid(), TenantId = tenantId, ConsumerId = consumerId };
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "quota.tenant_id"),
            Guard.NotEmpty(consumerId, "quota.consumer_id"))
            ?? quota.Apply(tokensPerMinute, tokensPerMonth, maxConcurrentRequests);
        return error is null ? quota : error;
    }

    public Result Update(int? tokensPerMinute, long? tokensPerMonth, int? maxConcurrentRequests)
    {
        var error = Apply(tokensPerMinute, tokensPerMonth, maxConcurrentRequests);
        return error is null ? Result.Success() : error;
    }

    private Error? Apply(int? tokensPerMinute, long? tokensPerMonth, int? maxConcurrentRequests)
    {
        var error = Guard.First(
            tokensPerMinute is { } tpm ? Guard.Positive(tpm, "quota.tokens_per_minute") : null,
            tokensPerMonth is { } tpmo ? Guard.Positive(tpmo, "quota.tokens_per_month") : null,
            maxConcurrentRequests is { } mcr ? Guard.Positive(mcr, "quota.max_concurrent_requests") : null);
        if (error is not null)
        {
            return error;
        }

        TokensPerMinute = tokensPerMinute;
        TokensPerMonth = tokensPerMonth;
        MaxConcurrentRequests = maxConcurrentRequests;
        return null;
    }
}
