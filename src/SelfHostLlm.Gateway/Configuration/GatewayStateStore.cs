using Microsoft.Extensions.Primitives;
using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Gateway.Configuration;

/// <summary>
/// Giữ <see cref="GatewayState"/> hiện hành (thay nguyên khối, thread-safe) và phát change token
/// để cấu hình YARP dựng lại cluster khi deployment thay đổi.
/// </summary>
internal sealed class GatewayStateStore : IMonthlyUsageBaseline, IDisposable
{
    private GatewayState? _current;
    private CancellationTokenSource _changeSource = new();

    public GatewayState? Current => Volatile.Read(ref _current);

    public IChangeToken ChangeToken => new CancellationChangeToken(Volatile.Read(ref _changeSource).Token);

    public void Replace(GatewayState state)
    {
        Volatile.Write(ref _current, state);
        var previous = Interlocked.Exchange(ref _changeSource, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    public void Dispose() => _changeSource.Dispose();

    /// <summary>Baseline usage tháng cho quota — lấy từ snapshot (CP tính từ DB).</summary>
    public MonthlyUsage Get(Guid consumerId) => Current?.MonthlyUsageFor(consumerId) ?? MonthlyUsage.None;
}
