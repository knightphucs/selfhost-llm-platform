using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Enforce quota ở gateway. Kiểm <b>trước</b> khi gọi engine dựa trên bộ đếm hiện tại, cập nhật
/// <b>sau</b> khi có usage thật — best-effort, không phải transaction chặt: một request vượt
/// ngưỡng giữa chừng vẫn chạy xong.
/// </summary>
public interface IQuotaService
{
    /// <summary>
    /// Kiểm concurrency → rate → budget. Thành công trả lease giữ một slot concurrency; caller
    /// phải Dispose lease khi request kết thúc. Vượt quota trả <see cref="ErrorKind.RateLimited"/>.
    /// </summary>
    Result<QuotaLease> TryAcquire(Guid consumerId, QuotaLimits limits);

    /// <summary>Ghi nhận token thực tế sau khi request xong (prompt + completion).</summary>
    void RecordUsage(Guid consumerId, long tokens);
}

/// <summary>Giới hạn lấy từ config snapshot. <c>null</c> = không giới hạn chiều đó.</summary>
public sealed record QuotaLimits(int? TokensPerMinute, long? TokensPerMonth, int? MaxConcurrentRequests)
{
    public static QuotaLimits Unlimited { get; } = new(null, null, null);
}

/// <summary>Giữ một slot concurrency. Dispose nhiều lần chỉ trả slot một lần.</summary>
public sealed class QuotaLease : IDisposable
{
    private Action? _release;

    internal QuotaLease(Action release)
    {
        _release = release;
    }

    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

/// <summary>
/// Usage tháng hiện tại đã được lưu bền (tổng hợp từ DB), tính tới thời điểm
/// <see cref="MonthlyUsage.AsOf"/>. Gateway lấy từ config snapshot.
/// </summary>
public interface IMonthlyUsageBaseline
{
    MonthlyUsage Get(Guid consumerId);
}

public sealed record MonthlyUsage(long Tokens, DateTimeOffset AsOf)
{
    public static MonthlyUsage None { get; } = new(0, DateTimeOffset.MinValue);
}
