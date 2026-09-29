using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Auditing;

/// <summary>Đọc audit log (append-only — không có command sửa/xoá tương ứng).</summary>
public sealed record ListAuditLogsQuery(
    Guid TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? EntityType,
    string? EntityId,
    Guid? ActorUserId,
    PageRequest Page) : IRequest<Result<PagedResult<AuditLog>>>, ITenantRequest;

internal sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        RuleFor(q => q.Page).ValidPage();
        RuleFor(q => q.EntityType).MaximumLength(100);
        RuleFor(q => q.EntityId).MaximumLength(100);
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From < q.To)
            .WithName("From").WithMessage("From phải trước To.");
    }
}

internal sealed class ListAuditLogsHandler(IAuditLogQueries auditLogs)
    : IRequestHandler<ListAuditLogsQuery, Result<PagedResult<AuditLog>>>
{
    public async Task<Result<PagedResult<AuditLog>>> HandleAsync(ListAuditLogsQuery request, CancellationToken cancellationToken) =>
        Result<PagedResult<AuditLog>>.Success(await auditLogs.ListAsync(
            request.TenantId,
            new AuditLogFilter(request.From, request.To, request.EntityType, request.EntityId, request.ActorUserId),
            request.Page,
            cancellationToken));
}
