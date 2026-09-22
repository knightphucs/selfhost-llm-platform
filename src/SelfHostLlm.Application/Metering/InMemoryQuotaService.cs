using System.Collections.Concurrent;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Metering;

/// <summary>
/// Quota in-memory cho một instance gateway.
/// <list type="bullet">
/// <item>Rate: tổng token trong 60 giây gần nhất.</item>
/// <item>Budget: baseline tháng từ DB + token in-memory ghi nhận sau thời điểm baseline.</item>
/// <item>Concurrency: số request đang chạy.</item>
/// </list>
/// Giới hạn (ghi trong báo cáo): chỉ đúng khi chạy một instance; bộ đếm mất khi restart
/// (budget hồi lại nhờ baseline); chưa tính token của chính request đang tới.
/// </summary>
internal sealed class InMemoryQuotaService(TimeProvider timeProvider, IMonthlyUsageBaseline baseline) : IQuotaService
{
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<Guid, ConsumerState> _states = new();

    public Result<QuotaLease> TryAcquire(Guid consumerId, QuotaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var now = timeProvider.GetUtcNow();
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var persisted = baseline.Get(consumerId);

        // Baseline của tháng trước không tính; entry in-memory chỉ đếm phần sau thời điểm baseline
        // để không cộng trùng những gì DB đã có.
        var baselineTokens = persisted.AsOf >= monthStart ? persisted.Tokens : 0;
        var budgetSince = persisted.AsOf > monthStart ? persisted.AsOf : monthStart;

        var state = _states.GetOrAdd(consumerId, static _ => new ConsumerState());
        lock (state.Gate)
        {
            state.Prune(Min(now - RateWindow, budgetSince));

            if (limits.MaxConcurrentRequests is { } maxConcurrent && state.Active >= maxConcurrent)
            {
                return Error.RateLimited(
                    "quota.concurrency_exceeded",
                    $"Vượt số request đồng thời cho phép ({maxConcurrent}).");
            }

            if (limits.TokensPerMinute is { } perMinute && state.TokensSince(now - RateWindow) >= perMinute)
            {
                return Error.RateLimited(
                    "quota.rate_exceeded",
                    $"Vượt giới hạn {perMinute} token/phút. Thử lại sau ít giây.");
            }

            if (limits.TokensPerMonth is { } perMonth && baselineTokens + state.TokensSince(budgetSince) >= perMonth)
            {
                return Error.RateLimited(
                    "quota.budget_exceeded",
                    $"Đã dùng hết ngân sách {perMonth} token của tháng.");
            }

            state.Active++;
        }

        return new QuotaLease(() =>
        {
            lock (state.Gate)
            {
                state.Active--;
            }
        });
    }

    public void RecordUsage(Guid consumerId, long tokens)
    {
        if (tokens <= 0)
        {
            return;
        }

        var state = _states.GetOrAdd(consumerId, static _ => new ConsumerState());
        lock (state.Gate)
        {
            state.Entries.Enqueue(new UsageEntry(timeProvider.GetUtcNow(), tokens));
        }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private readonly record struct UsageEntry(DateTimeOffset At, long Tokens);

    private sealed class ConsumerState
    {
        public object Gate { get; } = new();

        /// <summary>Entry theo thứ tự thời gian ghi nhận.</summary>
        public Queue<UsageEntry> Entries { get; } = new();

        public int Active { get; set; }

        public long TokensSince(DateTimeOffset since) => Entries.Where(e => e.At >= since).Sum(e => e.Tokens);

        /// <summary>Bỏ entry không còn thuộc cửa sổ rate lẫn cửa sổ budget.</summary>
        public void Prune(DateTimeOffset cutoff)
        {
            while (Entries.Count > 0 && Entries.Peek().At < cutoff)
            {
                Entries.Dequeue();
            }
        }
    }
}
