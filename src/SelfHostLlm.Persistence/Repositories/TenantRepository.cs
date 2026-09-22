using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Persistence.Repositories;

/// <summary>
/// Điểm lọc tenant DUY NHẤT cho entity <see cref="ITenantScoped"/>: mọi truy vấn đi qua
/// <see cref="ForTenant"/>, nơi <c>tenant_id = @tenantId</c> được áp trước mọi điều kiện khác.
/// </summary>
internal sealed class TenantRepository<T>(AppDbContext db) : ITenantRepository<T>
    where T : Entity<Guid>, ITenantScoped
{
    public Task<T?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        ForTenant(tenantId).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(Guid tenantId, Expression<Func<T, bool>>? filter, CancellationToken cancellationToken)
    {
        var query = ForTenant(tenantId);
        if (filter is not null)
        {
            query = query.Where(filter);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid tenantId, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        ForTenant(tenantId).AnyAsync(predicate, cancellationToken);

    public void Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.TenantId == Guid.Empty)
        {
            throw new ArgumentException($"{typeof(T).Name} phải thuộc một tenant.", nameof(entity));
        }

        db.Set<T>().Add(entity);
    }

    public void Remove(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        db.Set<T>().Remove(entity);
    }

    private IQueryable<T> ForTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId không được rỗng.", nameof(tenantId));
        }

        return db.Set<T>().Where(e => e.TenantId == tenantId);
    }
}
