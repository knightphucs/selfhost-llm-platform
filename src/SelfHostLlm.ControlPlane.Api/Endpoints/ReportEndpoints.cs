using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Usage;
using SelfHostLlm.ControlPlane.Api.Http;
using SelfHostLlm.ControlPlane.Api.Mapping;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

/// <summary>Usage và audit log — chỉ đọc. Audit log không có endpoint sửa/xoá.</summary>
internal static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Routes.Tenant).WithTags("Reports");

        group.MapGet("/usage", async (
                    Guid tenantId, DateTimeOffset? from, DateTimeOffset? to, Guid? apiKeyId, Guid? deploymentId,
                    int? page, int? pageSize, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListUsageRecordsQuery(tenantId, from, to, apiKeyId, deploymentId, Page(page, pageSize)), ct))
                .ToOk(p => p.ToResponse(u => u.ToResponse())))
            .RequireAuthorization(Permissions.UsageRead);

        group.MapGet("/usage/summary", async (
                    Guid tenantId, Guid? consumerId, DateTimeOffset from, DateTimeOffset to, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetUsageSummaryQuery(tenantId, consumerId, from, to), ct)).ToOk(s => s.ToResponse()))
            .RequireAuthorization(Permissions.UsageRead);

        group.MapGet("/audit-logs", async (
                    Guid tenantId, DateTimeOffset? from, DateTimeOffset? to, string? entityType, string? entityId, Guid? actorUserId,
                    int? page, int? pageSize, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListAuditLogsQuery(tenantId, from, to, entityType, entityId, actorUserId, Page(page, pageSize)), ct))
                .ToOk(p => p.ToResponse(a => a.ToResponse())))
            .RequireAuthorization(Permissions.AuditRead);

        return app;
    }

    private static PageRequest Page(int? page, int? pageSize) =>
        new(page ?? 1, pageSize ?? PageRequest.DefaultPageSize);
}
