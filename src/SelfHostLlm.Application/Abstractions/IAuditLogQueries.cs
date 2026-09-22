using SelfHostLlm.Application.Common;
using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Application.Abstractions;

public interface IAuditLogQueries
{
    Task<PagedResult<AuditLog>> ListAsync(Guid tenantId, AuditLogFilter filter, PageRequest page, CancellationToken cancellationToken);
}

public sealed record AuditLogFilter(DateTimeOffset? From, DateTimeOffset? To, string? EntityType, string? EntityId, Guid? ActorUserId);
