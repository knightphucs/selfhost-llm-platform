using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.Tenants;

/// <summary>Xem tenant — người trong tenant đó hoặc PlatformAdmin.</summary>
public sealed record GetTenantQuery(Guid TenantId) : IRequest<Result<Tenant>>, ITenantRequest;

internal sealed class GetTenantHandler(ITenantStore tenants) : IRequestHandler<GetTenantQuery, Result<Tenant>>
{
    public async Task<Result<Tenant>> HandleAsync(GetTenantQuery request, CancellationToken cancellationToken) =>
        await tenants.GetAsync(request.TenantId, cancellationToken) is { } tenant
            ? tenant
            : UseCaseExtensions.NotFound<Tenant>(request.TenantId);
}

/// <summary>Liệt kê mọi tenant — chỉ PlatformAdmin.</summary>
public sealed record ListTenantsQuery : IRequest<Result<IReadOnlyList<Tenant>>>, IPlatformRequest;

internal sealed class ListTenantsHandler(ITenantStore tenants) : IRequestHandler<ListTenantsQuery, Result<IReadOnlyList<Tenant>>>
{
    public async Task<Result<IReadOnlyList<Tenant>>> HandleAsync(ListTenantsQuery request, CancellationToken cancellationToken) =>
        Result<IReadOnlyList<Tenant>>.Success(await tenants.ListAsync(cancellationToken));
}
