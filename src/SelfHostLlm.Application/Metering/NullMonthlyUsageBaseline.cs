using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Application.Metering;

/// <summary>Mặc định khi chưa có baseline từ snapshot: budget chỉ dựa vào bộ đếm in-memory.</summary>
internal sealed class NullMonthlyUsageBaseline : IMonthlyUsageBaseline
{
    public MonthlyUsage Get(Guid consumerId) => MonthlyUsage.None;
}
