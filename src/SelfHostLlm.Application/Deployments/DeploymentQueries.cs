using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;

namespace SelfHostLlm.Application.Deployments;

public sealed record GetDeploymentQuery(Guid TenantId, Guid DeploymentId) : IRequest<Result<Deployment>>, ITenantRequest;

/// <param name="ModelId">Lọc theo model (tuỳ chọn).</param>
public sealed record ListDeploymentsQuery(Guid TenantId, Guid? ModelId = null) : IRequest<Result<IReadOnlyList<Deployment>>>, ITenantRequest;

internal sealed class GetDeploymentHandler(ITenantRepository<Deployment> deployments)
    : IRequestHandler<GetDeploymentQuery, Result<Deployment>>
{
    public async Task<Result<Deployment>> HandleAsync(GetDeploymentQuery request, CancellationToken cancellationToken) =>
        await deployments.GetAsync(request.TenantId, request.DeploymentId, cancellationToken) is { } deployment
            ? deployment
            : UseCaseExtensions.NotFound<Deployment>(request.DeploymentId);
}

internal sealed class ListDeploymentsHandler(ITenantRepository<Deployment> deployments)
    : IRequestHandler<ListDeploymentsQuery, Result<IReadOnlyList<Deployment>>>
{
    public async Task<Result<IReadOnlyList<Deployment>>> HandleAsync(ListDeploymentsQuery request, CancellationToken cancellationToken)
    {
        var list = request.ModelId is { } modelId
            ? await deployments.ListAsync(request.TenantId, d => d.ModelId == modelId, cancellationToken)
            : await deployments.ListAsync(request.TenantId, null, cancellationToken);
        return Result<IReadOnlyList<Deployment>>.Success(list.OrderBy(d => d.RemoteModelName, StringComparer.Ordinal).ThenBy(d => d.Id).ToList());
    }
}
