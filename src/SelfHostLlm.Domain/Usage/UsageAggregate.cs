using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Usage;

/// <summary>
/// Tổng hợp usage theo consumer và khoảng thời gian, do Worker.Health tính định kỳ.
/// Enforce quota đọc bảng này cộng bộ đếm in-memory, không quét toàn bộ UsageRecord.
/// </summary>
public sealed class UsageAggregate : Entity<long>, ITenantScoped
{
    private UsageAggregate()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ConsumerId { get; private set; }

    public UsagePeriod Period { get; private set; }

    /// <summary>Đầu bucket (UTC), đã căn theo <see cref="Period"/>.</summary>
    public DateTimeOffset BucketStart { get; private set; }

    public long PromptTokens { get; private set; }

    public long CompletionTokens { get; private set; }

    public int RequestCount { get; private set; }

    public static Result<UsageAggregate> Create(Guid tenantId, Guid consumerId, UsagePeriod period, DateTimeOffset at)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "usage_aggregate.tenant_id"),
            Guard.NotEmpty(consumerId, "usage_aggregate.consumer_id"),
            Enum.IsDefined(period) ? null : Error.Validation("usage_aggregate.period.invalid", "UsagePeriod không hợp lệ."));
        if (error is not null)
        {
            return error;
        }

        return new UsageAggregate
        {
            TenantId = tenantId,
            ConsumerId = consumerId,
            Period = period,
            BucketStart = BucketStartFor(period, at),
        };
    }

    /// <summary>Căn thời điểm <paramref name="at"/> về đầu bucket UTC của <paramref name="period"/>.</summary>
    public static DateTimeOffset BucketStartFor(UsagePeriod period, DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return period switch
        {
            UsagePeriod.Hour => new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero),
            UsagePeriod.Day => new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero),
            UsagePeriod.Month => new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
        };
    }

    public void Add(TokenCount tokens, int requestCount = 1)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentOutOfRangeException.ThrowIfNegative(requestCount);

        PromptTokens += tokens.Prompt;
        CompletionTokens += tokens.Completion;
        RequestCount += requestCount;
    }
}
