using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Models;

namespace SelfHostLlm.Application.Models;

public sealed record GetModelQuery(Guid TenantId, Guid ModelId) : IRequest<Result<Model>>, ITenantRequest;

internal sealed class GetModelHandler(ITenantRepository<Model> models) : IRequestHandler<GetModelQuery, Result<Model>>
{
    public async Task<Result<Model>> HandleAsync(GetModelQuery request, CancellationToken cancellationToken) =>
        await models.GetAsync(request.TenantId, request.ModelId, cancellationToken) is { } model
            ? model
            : UseCaseExtensions.NotFound<Model>(request.ModelId);
}

public sealed record ListModelsQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<Model>>>, ITenantRequest;

internal sealed class ListModelsHandler(ITenantRepository<Model> models) : IRequestHandler<ListModelsQuery, Result<IReadOnlyList<Model>>>
{
    public async Task<Result<IReadOnlyList<Model>>> HandleAsync(ListModelsQuery request, CancellationToken cancellationToken)
    {
        var list = await models.ListAsync(request.TenantId, null, cancellationToken);
        return Result<IReadOnlyList<Model>>.Success(list.OrderBy(m => m.Name, StringComparer.Ordinal).ToList());
    }
}
