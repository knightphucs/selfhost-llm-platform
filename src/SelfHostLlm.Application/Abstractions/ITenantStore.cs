using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>Truy cập <see cref="Tenant"/> — gốc cô lập, không thuộc tenant nào nên không đi qua <see cref="ITenantRepository{T}"/>.</summary>
public interface ITenantStore
{
    Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Tenant tenant);
}
