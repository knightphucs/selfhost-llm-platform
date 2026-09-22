using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.Tenants;

/// <summary>Tạo tenant mới — chỉ PlatformAdmin.</summary>
public sealed record CreateTenantCommand(string Name, string Slug) : IRequest<Result<Tenant>>, IPlatformRequest;

internal sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Slug).NotEmpty().Matches("^[a-z0-9-]{2,50}$").WithMessage("Slug chỉ gồm a-z, 0-9, '-' và dài 2–50 ký tự.");
    }
}

internal sealed class CreateTenantHandler(ITenantStore tenants, IAuditTrail audit, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<CreateTenantCommand, Result<Tenant>>
{
    public async Task<Result<Tenant>> HandleAsync(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (await tenants.SlugExistsAsync(request.Slug, cancellationToken))
        {
            return Error.Conflict("tenant.slug_taken", $"Slug '{request.Slug}' đã được dùng.");
        }

        var created = Tenant.Create(request.Name, request.Slug, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created;
        }

        var tenant = created.Value;
        tenants.Add(tenant);
        audit.Record(tenant.Id, AuditAction.Create, nameof(Tenant), tenant.Id.ToString(), null, AuditSnapshot.Of(tenant));
        return await unitOfWork.SaveAndReturnAsync(tenant, cancellationToken);
    }
}
