using SelfHostLlm.Domain.Usage;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Nơi Gateway đẩy <see cref="UsageRecord"/> sau mỗi request. Ghi async, batch, NGOÀI critical
/// path (QĐ-2): không bao giờ chặn response, lỗi ghi chỉ được log.
/// </summary>
public interface IUsageSink
{
    /// <summary>Trả false khi hàng đợi đầy (bản ghi bị bỏ) — caller chỉ log, không báo lỗi cho client.</summary>
    bool TryWrite(UsageRecord record);
}
