using System.Threading.Channels;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Persistence;

namespace SelfHostLlm.Gateway.Metering;

/// <summary>Hàng đợi usage có giới hạn — đầy thì bỏ bản ghi mới (không chặn request).</summary>
internal sealed class ChannelUsageSink : IUsageSink
{
    public const int Capacity = 10_000;

    private readonly Channel<UsageRecord> _channel = Channel.CreateBounded<UsageRecord>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<UsageRecord> Reader => _channel.Reader;

    public bool TryWrite(UsageRecord record) => _channel.Writer.TryWrite(record);

    public void Complete() => _channel.Writer.TryComplete();
}

/// <summary>
/// Ghi usage thẳng vào Postgres theo batch (tối đa 100 bản ghi hoặc sau 2 giây), ngoài critical
/// path (QĐ-2). Đây là một trong hai bảng duy nhất Gateway được ghi (bất biến 2). CP chết vẫn ghi được.
/// </summary>
internal sealed partial class UsageFlushService(
    ChannelUsageSink sink,
    IServiceScopeFactory scopeFactory,
    ILogger<UsageFlushService> logger) : BackgroundService
{
    public const int MaxBatchSize = 100;
    public static readonly TimeSpan MaxBatchDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<UsageRecord>(MaxBatchSize);
        try
        {
            while (await sink.Reader.WaitToReadAsync(stoppingToken))
            {
                using var window = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                window.CancelAfter(MaxBatchDelay);
                try
                {
                    while (batch.Count < MaxBatchSize && await sink.Reader.WaitToReadAsync(window.Token))
                    {
                        while (batch.Count < MaxBatchSize && sink.Reader.TryRead(out var record))
                        {
                            batch.Add(record);
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Hết cửa sổ gom batch.
                }

                await FlushAsync(batch, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Dừng host: xả nốt phần còn lại.
            while (sink.Reader.TryRead(out var record))
            {
                batch.Add(record);
            }

            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<UsageRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UsageRecords.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ghi usage lỗi không được ảnh hưởng client — chỉ log (không log nội dung request).
            LogFlushFailed(logger, ex, batch.Count);
        }
        finally
        {
            batch.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Ghi {Count} usage record thất bại")]
    private static partial void LogFlushFailed(ILogger logger, Exception exception, int count);
}
