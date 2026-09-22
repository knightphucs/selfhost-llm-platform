using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Common.Behaviors;

/// <summary>
/// Lớp chặn cô lập tenant ở Application, chạy trước validation và handler:
/// <list type="bullet">
/// <item><see cref="ITenantRequest"/>: người gọi phải cùng tenant, hoặc là PlatformAdmin.</item>
/// <item><see cref="IPlatformRequest"/>: chỉ PlatformAdmin.</item>
/// </list>
/// Độc lập với RBAC permission ở endpoint — kể cả khi endpoint cấu hình sai, request vẫn không
/// chạm được dữ liệu tenant khác.
/// </summary>
internal sealed class TenantAccessBehavior<TRequest, TResponse>(ICurrentActor actor)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
    public Task<TResponse> HandleAsync(TRequest request, NextHandler<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is IPlatformRequest && !actor.IsPlatformAdmin)
        {
            return Deny("tenant_access.platform_only", "Chỉ PlatformAdmin được thực hiện thao tác này.");
        }

        if (request is ITenantRequest scoped)
        {
            if (scoped.TenantId == Guid.Empty)
            {
                return Task.FromResult(TResponse.FromError(Error.Validation("tenant_access.tenant_id.empty", "Thiếu tenantId.")));
            }

            if (!actor.IsPlatformAdmin && scoped.TenantId != actor.TenantId)
            {
                return Deny("tenant_access.forbidden", "Không có quyền truy cập dữ liệu của tenant này.");
            }
        }

        return next();
    }

    private static Task<TResponse> Deny(string code, string message) =>
        Task.FromResult(TResponse.FromError(Error.Forbidden(code, message)));
}
