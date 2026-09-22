using Microsoft.Extensions.Time.Testing;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Metering;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Metering;

public sealed class InMemoryQuotaServiceTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeBaseline _baseline = new();
    private readonly InMemoryQuotaService _quota;
    private readonly Guid _consumer = Guid.NewGuid();

    public InMemoryQuotaServiceTests()
    {
        _quota = new InMemoryQuotaService(_clock, _baseline);
    }

    private sealed class FakeBaseline : IMonthlyUsageBaseline
    {
        public MonthlyUsage Current { get; set; } = MonthlyUsage.None;

        public MonthlyUsage Get(Guid consumerId) => Current;
    }

    private Result<QuotaLease> Acquire(QuotaLimits limits, Guid? consumer = null) =>
        _quota.TryAcquire(consumer ?? _consumer, limits);

    [Fact]
    public void TryAcquire_WithoutLimits_AlwaysSucceeds()
    {
        _quota.RecordUsage(_consumer, 10_000_000);

        for (var i = 0; i < 50; i++)
        {
            Acquire(QuotaLimits.Unlimited).IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public void TryAcquire_AtConcurrencyLimit_RejectsUntilLeaseDisposed()
    {
        var limits = new QuotaLimits(null, null, MaxConcurrentRequests: 1);
        var first = Acquire(limits);

        var second = Acquire(limits);
        second.Error!.Kind.Should().Be(ErrorKind.RateLimited);
        second.Error.Code.Should().Be("quota.concurrency_exceeded");

        first.Value.Dispose();
        Acquire(limits).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void LeaseDispose_CalledTwice_ReleasesSlotOnlyOnce()
    {
        var limits = new QuotaLimits(null, null, MaxConcurrentRequests: 2);
        var first = Acquire(limits).Value;
        Acquire(limits).IsSuccess.Should().BeTrue();

        first.Dispose();
        first.Dispose();

        Acquire(limits).IsSuccess.Should().BeTrue();
        Acquire(limits).Error!.Code.Should().Be("quota.concurrency_exceeded");
    }

    [Fact]
    public void TryAcquire_AfterUsingTokensPerMinute_RejectsThenRecoversAfterWindow()
    {
        var limits = new QuotaLimits(TokensPerMinute: 1000, null, null);
        _quota.RecordUsage(_consumer, 600);
        Acquire(limits).IsSuccess.Should().BeTrue();

        _clock.Advance(TimeSpan.FromSeconds(30));
        _quota.RecordUsage(_consumer, 400);
        Acquire(limits).Error!.Code.Should().Be("quota.rate_exceeded");

        // Entry 600 token rơi khỏi cửa sổ 60 giây, còn lại 400 < 1000.
        _clock.Advance(TimeSpan.FromSeconds(31));
        Acquire(limits).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_BudgetCountsBaselinePlusInMemoryUsage()
    {
        var limits = new QuotaLimits(null, TokensPerMonth: 10_000, null);
        _baseline.Current = new MonthlyUsage(9_000, _clock.GetUtcNow().AddMinutes(-5));

        Acquire(limits).IsSuccess.Should().BeTrue();
        _quota.RecordUsage(_consumer, 999);
        Acquire(limits).IsSuccess.Should().BeTrue();

        _quota.RecordUsage(_consumer, 1);
        var result = Acquire(limits);
        result.Error!.Kind.Should().Be(ErrorKind.RateLimited);
        result.Error.Code.Should().Be("quota.budget_exceeded");
    }

    [Fact]
    public void TryAcquire_NewerBaseline_DoesNotDoubleCountUsageAlreadyPersisted()
    {
        var limits = new QuotaLimits(null, TokensPerMonth: 10_000, null);
        _quota.RecordUsage(_consumer, 6_000);
        _clock.Advance(TimeSpan.FromMinutes(1));

        // Snapshot mới: DB đã tổng hợp 6 000 token trên (AsOf sau thời điểm ghi nhận).
        _baseline.Current = new MonthlyUsage(6_000, _clock.GetUtcNow());

        Acquire(limits).IsSuccess.Should().BeTrue("6 000 chỉ được tính một lần, không phải 12 000");
        _quota.RecordUsage(_consumer, 4_000);
        Acquire(limits).Error!.Code.Should().Be("quota.budget_exceeded");
    }

    [Fact]
    public void TryAcquire_NewMonth_ResetsBudgetAndIgnoresLastMonthBaseline()
    {
        var limits = new QuotaLimits(null, TokensPerMonth: 1_000, null);
        _baseline.Current = new MonthlyUsage(900, _clock.GetUtcNow());
        _quota.RecordUsage(_consumer, 200);
        Acquire(limits).Error!.Code.Should().Be("quota.budget_exceeded");

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 0, 0, 1, TimeSpan.Zero));

        Acquire(limits).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_UsageOfOneConsumer_DoesNotAffectAnother()
    {
        var limits = new QuotaLimits(TokensPerMinute: 100, TokensPerMonth: 100, MaxConcurrentRequests: 1);
        _quota.RecordUsage(_consumer, 1_000);
        Acquire(limits).IsFailure.Should().BeTrue();

        Acquire(limits, Guid.NewGuid()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_WhenRejected_DoesNotHoldConcurrencySlot()
    {
        var limits = new QuotaLimits(TokensPerMinute: 10, null, MaxConcurrentRequests: 1);
        _quota.RecordUsage(_consumer, 10);
        Acquire(limits).Error!.Code.Should().Be("quota.rate_exceeded");

        _clock.Advance(TimeSpan.FromMinutes(2));

        Acquire(limits).IsSuccess.Should().BeTrue("request bị từ chối trước đó không được giữ slot");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RecordUsage_NonPositiveTokens_IsIgnored(long tokens)
    {
        var limits = new QuotaLimits(TokensPerMinute: 1, null, null);

        _quota.RecordUsage(_consumer, tokens);

        Acquire(limits).IsSuccess.Should().BeTrue();
    }
}
