using System.Linq.Expressions;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Fakes;

/// <summary>Repository in-memory — cùng hợp đồng lọc tenant với bản EF.</summary>
internal sealed class FakeTenantRepository<T> : ITenantRepository<T>
    where T : Entity<Guid>, ITenantScoped
{
    public List<T> Items { get; } = [];

    public Task<T?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(ForTenant(tenantId).FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<T>> ListAsync(Guid tenantId, Expression<Func<T, bool>>? filter, CancellationToken cancellationToken)
    {
        var query = ForTenant(tenantId);
        if (filter is not null)
        {
            query = query.Where(filter.Compile());
        }

        return Task.FromResult<IReadOnlyList<T>>(query.ToList());
    }

    public Task<bool> ExistsAsync(Guid tenantId, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(ForTenant(tenantId).Any(predicate.Compile()));

    public void Add(T entity)
    {
        if (entity.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId rỗng.", nameof(entity));
        }

        Items.Add(entity);
    }

    public void Remove(T entity) => Items.Remove(entity);

    private IEnumerable<T> ForTenant(Guid tenantId) => Items.Where(e => e.TenantId == tenantId);
}
