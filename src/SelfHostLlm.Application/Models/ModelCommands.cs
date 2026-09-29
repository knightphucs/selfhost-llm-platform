using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Models;

namespace SelfHostLlm.Application.Models;

public sealed record CreateModelCommand(
    Guid TenantId,
    string Name,
    string Family,
    string ParamSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TaskTags) : IRequest<Result<Model>>, ITenantRequest;

internal sealed class CreateModelValidator : AbstractValidator<CreateModelCommand>
{
    public CreateModelValidator()
    {
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Family).Name(100);
        RuleFor(c => c.ParamSize).Name(20);
        RuleFor(c => c.Quantization).Name(50);
        RuleFor(c => c.ContextLength).GreaterThan(0);
        RuleFor(c => c.Capabilities).NotEmpty();
        RuleForEach(c => c.Capabilities).MustBeEnumName<CreateModelCommand, ModelCapability>();
        RuleFor(c => c.TaskTags).NotNull();
        RuleForEach(c => c.TaskTags).MaximumLength(50);
    }
}

internal sealed class CreateModelHandler(ITenantRepository<Model> models, IAuditTrail audit, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<CreateModelCommand, Result<Model>>
{
    public async Task<Result<Model>> HandleAsync(CreateModelCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await models.ExistsAsync(request.TenantId, m => m.Name == name, cancellationToken))
        {
            return Error.Conflict("model.name_taken", $"Model '{name}' đã tồn tại trong tenant.");
        }

        var created = Model.Create(
            request.TenantId, name, request.Family, request.ParamSize, request.Quantization, request.ContextLength,
            request.Capabilities.Select(EnumText.Parse<ModelCapability>), request.TaskTags, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created;
        }

        var model = created.Value;
        models.Add(model);
        audit.Record(request.TenantId, AuditAction.Create, nameof(Model), model.Id.ToString(), null, AuditSnapshot.Of(model));
        return await unitOfWork.SaveAndReturnAsync(model, cancellationToken);
    }
}

public sealed record UpdateModelCommand(
    Guid TenantId,
    Guid ModelId,
    string Name,
    string Family,
    string ParamSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TaskTags) : IRequest<Result<Model>>, ITenantRequest;

internal sealed class UpdateModelValidator : AbstractValidator<UpdateModelCommand>
{
    public UpdateModelValidator()
    {
        RuleFor(c => c.ModelId).NotEmpty();
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Family).Name(100);
        RuleFor(c => c.ParamSize).Name(20);
        RuleFor(c => c.Quantization).Name(50);
        RuleFor(c => c.ContextLength).GreaterThan(0);
        RuleFor(c => c.Capabilities).NotEmpty();
        RuleForEach(c => c.Capabilities).MustBeEnumName<UpdateModelCommand, ModelCapability>();
        RuleFor(c => c.TaskTags).NotNull();
        RuleForEach(c => c.TaskTags).MaximumLength(50);
    }
}

internal sealed class UpdateModelHandler(ITenantRepository<Model> models, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateModelCommand, Result<Model>>
{
    public async Task<Result<Model>> HandleAsync(UpdateModelCommand request, CancellationToken cancellationToken)
    {
        if (await models.GetAsync(request.TenantId, request.ModelId, cancellationToken) is not { } model)
        {
            return UseCaseExtensions.NotFound<Model>(request.ModelId);
        }

        var name = request.Name.Trim();
        if (name != model.Name && await models.ExistsAsync(request.TenantId, m => m.Name == name, cancellationToken))
        {
            return Error.Conflict("model.name_taken", $"Model '{name}' đã tồn tại trong tenant.");
        }

        var before = AuditSnapshot.Of(model);
        var updated = model.Update(
            name, request.Family, request.ParamSize, request.Quantization, request.ContextLength,
            request.Capabilities.Select(EnumText.Parse<ModelCapability>), request.TaskTags);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(Model), model.Id.ToString(), before, AuditSnapshot.Of(model));
        return await unitOfWork.SaveAndReturnAsync(model, cancellationToken);
    }
}

/// <summary>Xoá model. Còn deployment/collection tham chiếu thì DB chặn (FK Restrict) → Conflict.</summary>
public sealed record DeleteModelCommand(Guid TenantId, Guid ModelId) : IRequest<Result>, ITenantRequest;

internal sealed class DeleteModelHandler(ITenantRepository<Model> models, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteModelCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteModelCommand request, CancellationToken cancellationToken)
    {
        if (await models.GetAsync(request.TenantId, request.ModelId, cancellationToken) is not { } model)
        {
            return UseCaseExtensions.NotFound<Model>(request.ModelId);
        }

        models.Remove(model);
        audit.Record(request.TenantId, AuditAction.Delete, nameof(Model), model.Id.ToString(), AuditSnapshot.Of(model), null);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
