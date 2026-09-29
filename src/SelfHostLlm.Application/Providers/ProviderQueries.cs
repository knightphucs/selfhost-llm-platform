using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Providers;

namespace SelfHostLlm.Application.Providers;

public sealed record GetProviderQuery(Guid TenantId, Guid ProviderId) : IRequest<Result<Provider>>, ITenantRequest;

public sealed record ListProvidersQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<Provider>>>, ITenantRequest;

internal sealed class GetProviderHandler(ITenantRepository<Provider> providers) : IRequestHandler<GetProviderQuery, Result<Provider>>
{
    public async Task<Result<Provider>> HandleAsync(GetProviderQuery request, CancellationToken cancellationToken) =>
        await providers.GetAsync(request.TenantId, request.ProviderId, cancellationToken) is { } provider
            ? provider
            : UseCaseExtensions.NotFound<Provider>(request.ProviderId);
}

internal sealed class ListProvidersHandler(ITenantRepository<Provider> providers)
    : IRequestHandler<ListProvidersQuery, Result<IReadOnlyList<Provider>>>
{
    public async Task<Result<IReadOnlyList<Provider>>> HandleAsync(ListProvidersQuery request, CancellationToken cancellationToken)
    {
        var list = await providers.ListAsync(request.TenantId, null, cancellationToken);
        return Result<IReadOnlyList<Provider>>.Success(list.OrderBy(p => p.Name, StringComparer.Ordinal).ToList());
    }
}
