using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Providers;

namespace SelfHostLlm.Application.Providers;

public sealed record CreateProviderCommand(Guid TenantId, string Name, string Kind, string? Description)
    : IRequest<Result<Provider>>, ITenantRequest;

public sealed record UpdateProviderCommand(Guid TenantId, Guid ProviderId, string Name, string Kind, string? Description)
    : IRequest<Result<Provider>>, ITenantRequest;

/// <summary>Xoá provider. Còn deployment tham chiếu thì DB chặn → Conflict.</summary>
public sealed record DeleteProviderCommand(Guid TenantId, Guid ProviderId) : IRequest<Result>, ITenantRequest;

internal sealed class CreateProviderValidator : AbstractValidator<CreateProviderCommand>
{
    public CreateProviderValidator()
    {
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Kind).MustBeEnumName<CreateProviderCommand, ProviderKind>();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

internal sealed class UpdateProviderValidator : AbstractValidator<UpdateProviderCommand>
{
    public UpdateProviderValidator()
    {
        RuleFor(c => c.ProviderId).NotEmpty();
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Kind).MustBeEnumName<UpdateProviderCommand, ProviderKind>();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

internal sealed class CreateProviderHandler(ITenantRepository<Provider> providers, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateProviderCommand, Result<Provider>>
{
    public async Task<Result<Provider>> HandleAsync(CreateProviderCommand request, CancellationToken cancellationToken)
    {
        var created = Provider.Create(request.TenantId, request.Name, EnumText.Parse<ProviderKind>(request.Kind), request.Description);
        if (created.IsFailure)
        {
            return created;
        }

        var provider = created.Value;
        providers.Add(provider);
        audit.Record(request.TenantId, AuditAction.Create, nameof(Provider), provider.Id.ToString(), null, AuditSnapshot.Of(provider));
        return await unitOfWork.SaveAndReturnAsync(provider, cancellationToken);
    }
}

internal sealed class UpdateProviderHandler(ITenantRepository<Provider> providers, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProviderCommand, Result<Provider>>
{
    public async Task<Result<Provider>> HandleAsync(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        if (await providers.GetAsync(request.TenantId, request.ProviderId, cancellationToken) is not { } provider)
        {
            return UseCaseExtensions.NotFound<Provider>(request.ProviderId);
        }

        var before = AuditSnapshot.Of(provider);
        var updated = provider.Update(request.Name, EnumText.Parse<ProviderKind>(request.Kind), request.Description);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(Provider), provider.Id.ToString(), before, AuditSnapshot.Of(provider));
        return await unitOfWork.SaveAndReturnAsync(provider, cancellationToken);
    }
}

internal sealed class DeleteProviderHandler(ITenantRepository<Provider> providers, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteProviderCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteProviderCommand request, CancellationToken cancellationToken)
    {
        if (await providers.GetAsync(request.TenantId, request.ProviderId, cancellationToken) is not { } provider)
        {
            return UseCaseExtensions.NotFound<Provider>(request.ProviderId);
        }

        providers.Remove(provider);
        audit.Record(request.TenantId, AuditAction.Delete, nameof(Provider), provider.Id.ToString(), AuditSnapshot.Of(provider), null);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
