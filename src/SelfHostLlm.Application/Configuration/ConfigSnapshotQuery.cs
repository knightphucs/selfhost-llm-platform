using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Configuration;

/// <summary>
/// Snapshot cấu hình cho Gateway. Không phải request theo tenant: endpoint nội bộ bảo vệ bằng
/// token dùng chung, không bằng danh tính người dùng.
/// </summary>
public sealed record GetConfigSnapshotQuery : IRequest<Result<ConfigSnapshotData>>;

internal sealed class GetConfigSnapshotHandler(IConfigSnapshotSource source, TimeProvider timeProvider)
    : IRequestHandler<GetConfigSnapshotQuery, Result<ConfigSnapshotData>>
{
    public async Task<Result<ConfigSnapshotData>> HandleAsync(GetConfigSnapshotQuery request, CancellationToken cancellationToken) =>
        Result<ConfigSnapshotData>.Success(await source.LoadAsync(timeProvider.GetUtcNow(), cancellationToken));
}
