using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Domain.Usage;

namespace SelfHostLlm.Persistence.Repositories;

internal sealed class UsageQueries(AppDbContext db) : IUsageQueries
{
    public async Task<PagedResult<UsageRecord>> ListAsync(
        Guid tenantId,
        UsageRecordFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);

        var query = db.UsageRecords.AsNoTracking().Where(u => u.TenantId == tenantId);
        if (filter.From is { } from)
        {
            query = query.Where(u => u.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(u => u.OccurredAt < to);
        }

        if (filter.ApiKeyId is { } apiKeyId)
        {
            query = query.Where(u => u.ApiKeyId == apiKeyId);
        }

        if (filter.DeploymentId is { } deploymentId)
        {
            query = query.Where(u => u.DeploymentId == deploymentId);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.OccurredAt)
            .ThenByDescending(u => u.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UsageRecord>(items, page.Page, page.PageSize, total);
    }

    public async Task<UsageSummary> SummaryAsync(
        Guid tenantId,
        Guid? consumerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var records = db.UsageRecords.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.OccurredAt >= from && u.OccurredAt < to);

        if (consumerId is { } consumer)
        {
            // Lọc consumer qua api_key; api_key cũng lọc tenant để join không bao giờ đi chéo tenant.
            records = records.Where(u => db.ApiKeys.Any(k => k.Id == u.ApiKeyId && k.TenantId == tenantId && k.ConsumerId == consumer));
        }

        var totals = await records
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Prompt = g.Sum(u => (long)u.PromptTokens),
                Completion = g.Sum(u => (long)u.CompletionTokens),
                Requests = g.LongCount(),
                Estimated = g.LongCount(u => u.TokensEstimated),
                Fallback = g.LongCount(u => u.UsedFallback),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? new UsageSummary(consumerId, from, to, 0, 0, 0, 0, 0)
            : new UsageSummary(consumerId, from, to, totals.Prompt, totals.Completion, totals.Requests, totals.Estimated, totals.Fallback);
    }
}
