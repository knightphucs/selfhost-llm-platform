using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Persistence.Configuration;

/// <summary>
/// Đọc toàn bộ cấu hình cho Gateway (mọi tenant — endpoint nội bộ). Usage tháng tính trực tiếp
/// từ <c>usage_record</c>; GĐ1 chuyển sang <c>usage_aggregate</c> do Worker.Health tổng hợp.
/// </summary>
internal sealed class ConfigSnapshotSource(AppDbContext db) : IConfigSnapshotSource
{
    public async Task<ConfigSnapshotData> LoadAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var models = await db.Models.AsNoTracking().OrderBy(m => m.Id).ToListAsync(cancellationToken);
        var deployments = await db.Deployments.AsNoTracking().OrderBy(d => d.Id).ToListAsync(cancellationToken);
        var virtualModels = await db.VirtualModels.AsNoTracking().OrderBy(v => v.Id).ToListAsync(cancellationToken);
        var routes = await db.Routes.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var quotas = await db.Quotas.AsNoTracking().OrderBy(q => q.Id).ToListAsync(cancellationToken);

        // Chỉ key dùng được: chưa thu hồi, chưa hết hạn, consumer (cùng tenant) đang bật.
        var apiKeys = await db.ApiKeys.AsNoTracking()
            .Where(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now))
            .Where(k => db.Consumers.Any(c => c.Id == k.ConsumerId && c.TenantId == k.TenantId && c.Enabled))
            .OrderBy(k => k.Id)
            .ToListAsync(cancellationToken);

        var monthlyUsage = await (
                from usage in db.UsageRecords
                where usage.OccurredAt >= monthStart && usage.OccurredAt < now
                join key in db.ApiKeys on new { usage.TenantId, Id = usage.ApiKeyId } equals new { key.TenantId, key.Id }
                group usage by key.ConsumerId into g
                orderby g.Key
                select new ConsumerMonthlyUsage(g.Key, g.Sum(u => (long)u.PromptTokens + u.CompletionTokens)))
            .ToListAsync(cancellationToken);

        return new ConfigSnapshotData(now, models, deployments, virtualModels, routes, apiKeys, quotas, monthlyUsage);
    }
}
