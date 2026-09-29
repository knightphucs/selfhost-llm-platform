using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Persistence.Repositories;

internal sealed class TenantStore(AppDbContext db) : ITenantStore
{
    public Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken) =>
        await db.Tenants.AsNoTracking().OrderBy(t => t.Slug).ToListAsync(cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);

    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

    public void Add(Tenant tenant) => db.Tenants.Add(tenant);
}
