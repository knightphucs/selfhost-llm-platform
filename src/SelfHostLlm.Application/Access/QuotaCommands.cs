using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Access;

/// <summary>Đặt quota cho consumer — tạo nếu chưa có. <c>null</c> = không giới hạn chiều đó.</summary>
public sealed record UpsertQuotaCommand(Guid TenantId, Guid ConsumerId, int? TokensPerMinute, long? TokensPerMonth, int? MaxConcurrentRequests)
    : IRequest<Result<Quota>>, ITenantRequest;

public sealed record GetQuotaQuery(Guid TenantId, Guid ConsumerId) : IRequest<Result<Quota>>, ITenantRequest;

internal sealed class UpsertQuotaValidator : AbstractValidator<UpsertQuotaCommand>
{
    public UpsertQuotaValidator()
    {
        RuleFor(c => c.ConsumerId).NotEmpty();
        RuleFor(c => c.TokensPerMinute).GreaterThan(0).When(c => c.TokensPerMinute is not null);
        RuleFor(c => c.TokensPerMonth).GreaterThan(0).When(c => c.TokensPerMonth is not null);
        RuleFor(c => c.MaxConcurrentRequests).GreaterThan(0).When(c => c.MaxConcurrentRequests is not null);
    }
}

internal sealed class UpsertQuotaHandler(
    ITenantRepository<Quota> quotas,
    ITenantRepository<Consumer> consumers,
    IAuditTrail audit,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertQuotaCommand, Result<Quota>>
{
    public async Task<Result<Quota>> HandleAsync(UpsertQuotaCommand request, CancellationToken cancellationToken)
    {
        if (!await consumers.ExistsAsync(request.TenantId, c => c.Id == request.ConsumerId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<Consumer>(request.ConsumerId);
        }

        var matches = await quotas.ListAsync(request.TenantId, q => q.ConsumerId == request.ConsumerId, cancellationToken);
        var existing = matches.Count > 0 ? matches[0] : null;
        if (existing is null)
        {
            var created = Quota.Create(request.TenantId, request.ConsumerId, request.TokensPerMinute, request.TokensPerMonth, request.MaxConcurrentRequests);
            if (created.IsFailure)
            {
                return created;
            }

            quotas.Add(created.Value);
            audit.Record(request.TenantId, AuditAction.Create, nameof(Quota), created.Value.Id.ToString(), null, AuditSnapshot.Of(created.Value));
            return await unitOfWork.SaveAndReturnAsync(created.Value, cancellationToken);
        }

        var before = AuditSnapshot.Of(existing);
        var updated = existing.Update(request.TokensPerMinute, request.TokensPerMonth, request.MaxConcurrentRequests);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(Quota), existing.Id.ToString(), before, AuditSnapshot.Of(existing));
        return await unitOfWork.SaveAndReturnAsync(existing, cancellationToken);
    }
}

internal sealed class GetQuotaHandler(ITenantRepository<Quota> quotas) : IRequestHandler<GetQuotaQuery, Result<Quota>>
{
    public async Task<Result<Quota>> HandleAsync(GetQuotaQuery request, CancellationToken cancellationToken)
    {
        var matches = await quotas.ListAsync(request.TenantId, q => q.ConsumerId == request.ConsumerId, cancellationToken);
        return matches.Count > 0
            ? matches[0]
            : Error.NotFound("quota.not_found", "Consumer chưa có quota (không giới hạn).");
    }
}
