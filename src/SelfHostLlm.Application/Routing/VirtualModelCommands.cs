using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.Routing;

public sealed record CreateVirtualModelCommand(Guid TenantId, string Name, string Task, string? Description)
    : IRequest<Result<VirtualModel>>, ITenantRequest;

public sealed record UpdateVirtualModelCommand(Guid TenantId, Guid VirtualModelId, string Name, string Task, string? Description)
    : IRequest<Result<VirtualModel>>, ITenantRequest;

/// <summary>Xoá virtual model. Còn route tham chiếu thì DB chặn → Conflict (xoá route trước).</summary>
public sealed record DeleteVirtualModelCommand(Guid TenantId, Guid VirtualModelId) : IRequest<Result>, ITenantRequest;

public sealed record GetVirtualModelQuery(Guid TenantId, Guid VirtualModelId) : IRequest<Result<VirtualModel>>, ITenantRequest;

public sealed record ListVirtualModelsQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<VirtualModel>>>, ITenantRequest;

internal sealed class CreateVirtualModelValidator : AbstractValidator<CreateVirtualModelCommand>
{
    public CreateVirtualModelValidator()
    {
        RuleFor(c => c.Name).NotEmpty().Matches("^[a-z0-9][a-z0-9-]{1,63}$")
            .WithMessage("Tên chỉ gồm a-z, 0-9, '-', bắt đầu bằng chữ/số, dài 2–64 ký tự (ví dụ code-fast).");
        RuleFor(c => c.Task).MustBeEnumName<CreateVirtualModelCommand, TaskKind>();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

internal sealed class UpdateVirtualModelValidator : AbstractValidator<UpdateVirtualModelCommand>
{
    public UpdateVirtualModelValidator()
    {
        RuleFor(c => c.VirtualModelId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().Matches("^[a-z0-9][a-z0-9-]{1,63}$")
            .WithMessage("Tên chỉ gồm a-z, 0-9, '-', bắt đầu bằng chữ/số, dài 2–64 ký tự (ví dụ code-fast).");
        RuleFor(c => c.Task).MustBeEnumName<UpdateVirtualModelCommand, TaskKind>();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

internal sealed class CreateVirtualModelHandler(ITenantRepository<VirtualModel> virtualModels, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateVirtualModelCommand, Result<VirtualModel>>
{
    public async Task<Result<VirtualModel>> HandleAsync(CreateVirtualModelCommand request, CancellationToken cancellationToken)
    {
        if (await virtualModels.ExistsAsync(request.TenantId, v => v.Name == request.Name, cancellationToken))
        {
            return Error.Conflict("virtual_model.name_taken", $"Virtual model '{request.Name}' đã tồn tại trong tenant.");
        }

        var created = VirtualModel.Create(request.TenantId, request.Name, EnumText.Parse<TaskKind>(request.Task), request.Description);
        if (created.IsFailure)
        {
            return created;
        }

        var virtualModel = created.Value;
        virtualModels.Add(virtualModel);
        audit.Record(request.TenantId, AuditAction.Create, nameof(VirtualModel), virtualModel.Id.ToString(), null, AuditSnapshot.Of(virtualModel));
        return await unitOfWork.SaveAndReturnAsync(virtualModel, cancellationToken);
    }
}

internal sealed class UpdateVirtualModelHandler(ITenantRepository<VirtualModel> virtualModels, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateVirtualModelCommand, Result<VirtualModel>>
{
    public async Task<Result<VirtualModel>> HandleAsync(UpdateVirtualModelCommand request, CancellationToken cancellationToken)
    {
        if (await virtualModels.GetAsync(request.TenantId, request.VirtualModelId, cancellationToken) is not { } virtualModel)
        {
            return UseCaseExtensions.NotFound<VirtualModel>(request.VirtualModelId);
        }

        if (request.Name != virtualModel.Name
            && await virtualModels.ExistsAsync(request.TenantId, v => v.Name == request.Name, cancellationToken))
        {
            return Error.Conflict("virtual_model.name_taken", $"Virtual model '{request.Name}' đã tồn tại trong tenant.");
        }

        var before = AuditSnapshot.Of(virtualModel);
        var updated = virtualModel.Update(request.Name, EnumText.Parse<TaskKind>(request.Task), request.Description);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(VirtualModel), virtualModel.Id.ToString(), before, AuditSnapshot.Of(virtualModel));
        return await unitOfWork.SaveAndReturnAsync(virtualModel, cancellationToken);
    }
}

internal sealed class DeleteVirtualModelHandler(ITenantRepository<VirtualModel> virtualModels, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteVirtualModelCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteVirtualModelCommand request, CancellationToken cancellationToken)
    {
        if (await virtualModels.GetAsync(request.TenantId, request.VirtualModelId, cancellationToken) is not { } virtualModel)
        {
            return UseCaseExtensions.NotFound<VirtualModel>(request.VirtualModelId);
        }

        virtualModels.Remove(virtualModel);
        audit.Record(request.TenantId, AuditAction.Delete, nameof(VirtualModel), virtualModel.Id.ToString(), AuditSnapshot.Of(virtualModel), null);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class GetVirtualModelHandler(ITenantRepository<VirtualModel> virtualModels)
    : IRequestHandler<GetVirtualModelQuery, Result<VirtualModel>>
{
    public async Task<Result<VirtualModel>> HandleAsync(GetVirtualModelQuery request, CancellationToken cancellationToken) =>
        await virtualModels.GetAsync(request.TenantId, request.VirtualModelId, cancellationToken) is { } virtualModel
            ? virtualModel
            : UseCaseExtensions.NotFound<VirtualModel>(request.VirtualModelId);
}

internal sealed class ListVirtualModelsHandler(ITenantRepository<VirtualModel> virtualModels)
    : IRequestHandler<ListVirtualModelsQuery, Result<IReadOnlyList<VirtualModel>>>
{
    public async Task<Result<IReadOnlyList<VirtualModel>>> HandleAsync(ListVirtualModelsQuery request, CancellationToken cancellationToken)
    {
        var list = await virtualModels.ListAsync(request.TenantId, null, cancellationToken);
        return Result<IReadOnlyList<VirtualModel>>.Success(list.OrderBy(v => v.Name, StringComparer.Ordinal).ToList());
    }
}
