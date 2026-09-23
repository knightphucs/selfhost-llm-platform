using Microsoft.Extensions.Logging;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Routing;

/// <summary>
/// Thử lần lượt các deployment trong <see cref="RoutePlan.Candidates"/>. Chỉ chuyển sang
/// deployment kế tiếp khi lỗi kết nối / timeout / 5xx / 408 / 429; 4xx khác trả thẳng về.
/// Retry nằm ở đây, KHÔNG ở tầng HTTP (Polly chỉ lo timeout + circuit breaker).
/// </summary>
public sealed partial class FallbackExecutor(ILogger<FallbackExecutor> logger, TimeProvider timeProvider)
{
    /// <summary>
    /// Chạy chuỗi fallback.
    /// </summary>
    /// <param name="attempt">
    /// Gọi một deployment. Hợp đồng: chỉ được trả <see cref="AttemptOutcome.Transient"/> khi
    /// <b>chưa ghi byte nào</b> cho client — một khi response đã commit thì không fallback được nữa
    /// (QĐ-3). Với YARP, lần thử thành công trả kết quả sau khi đã relay xong response; lần thử lỗi
    /// tạm thời bị chặn body từ trước nên chưa có byte nào đi ra.
    /// </param>
    /// <param name="cancellationToken">Token của request client. Bị huỷ thì dừng ngay, không thử tiếp.</param>
    public async Task<FallbackResult<T>> ExecuteAsync<T>(
        IReadOnlyList<DeploymentEntry> candidates,
        Func<DeploymentEntry, CancellationToken, Task<AttemptResult<T>>> attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(attempt);

        var attempts = new List<AttemptRecord>(candidates.Count);

        foreach (var deployment in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = timeProvider.GetTimestamp();

            AttemptResult<T> result;
            try
            {
                result = await attempt(deployment, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client ngắt kết nối — không phải lỗi của deployment, không thử tiếp.
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                result = AttemptResult<T>.Transient(null, ex.GetType().Name);
            }

            var record = new AttemptRecord(deployment.Id, result.Outcome, result.StatusCode, result.Reason, timeProvider.GetElapsedTime(started));
            attempts.Add(record);

            if (result.Outcome != AttemptOutcome.Transient)
            {
                return new FallbackResult<T> { Value = result.Value, ServedBy = deployment, Attempts = attempts };
            }

            LogAttemptFailed(logger, deployment.Id, record.StatusCode, record.Reason);
        }

        LogAllFailed(logger, attempts.Count);
        return new FallbackResult<T>
        {
            Attempts = attempts,
            Error = Error.Unavailable(
                "routing.all_deployments_failed",
                attempts.Count == 0
                    ? "Không có deployment nào để thử."
                    : $"Cả {attempts.Count} deployment đều không phản hồi được."),
        };
    }

    /// <summary>
    /// Lỗi hạ tầng đáng thử deployment khác. <see cref="OperationCanceledException"/> tới đây
    /// nghĩa là timeout (không phải client huỷ — trường hợp đó đã được bắt trước).
    /// Exception khác (bug) được ném ra ngoài.
    /// </summary>
    private static bool IsTransient(Exception exception) => exception
        is HttpRequestException
        or TimeoutException
        or IOException
        or UpstreamUnavailableException
        or OperationCanceledException;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deployment {DeploymentId} lỗi tạm thời (status {StatusCode}, {Reason}) — thử deployment kế tiếp")]
    private static partial void LogAttemptFailed(ILogger logger, Guid deploymentId, int? statusCode, string? reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hết chuỗi fallback sau {AttemptCount} lần thử")]
    private static partial void LogAllFailed(ILogger logger, int attemptCount);
}
