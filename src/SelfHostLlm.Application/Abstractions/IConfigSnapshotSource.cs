using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>Đọc toàn bộ cấu hình (mọi tenant) để phát cho Gateway. Chỉ dùng cho endpoint nội bộ.</summary>
public interface IConfigSnapshotSource
{
    Task<ConfigSnapshotData> LoadAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <param name="ApiKeys">Chỉ key chưa thu hồi, chưa hết hạn, của consumer đang bật.</param>
/// <param name="MonthlyUsage">Tổng token từ đầu tháng (UTC) tới <see cref="ConfigSnapshotData.GeneratedAt"/>, theo consumer.</param>
public sealed record ConfigSnapshotData(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<Model> Models,
    IReadOnlyList<Provider> Providers,
    IReadOnlyList<Deployment> Deployments,
    IReadOnlyList<VirtualModel> VirtualModels,
    IReadOnlyList<Route> Routes,
    IReadOnlyList<ApiKey> ApiKeys,
    IReadOnlyList<Quota> Quotas,
    IReadOnlyList<ConsumerMonthlyUsage> MonthlyUsage);

public sealed record ConsumerMonthlyUsage(Guid ConsumerId, long Tokens);
