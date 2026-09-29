using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Persistence.Repositories;

internal sealed class AuditLogQueries(AppDbContext db) : IAuditLogQueries
{
    public async Task<PagedResult<AuditLog>> ListAsync(
        Guid tenantId,
        AuditLogFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);

        var query = db.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);
        if (filter.From is { } from)
        {
            query = query.Where(a => a.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(a => a.OccurredAt < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(a => a.EntityId == filter.EntityId);
        }

        if (filter.ActorUserId is { } actor)
        {
            query = query.Where(a => a.ActorUserId == actor);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLog>(items, page.Page, page.PageSize, total);
    }
}
