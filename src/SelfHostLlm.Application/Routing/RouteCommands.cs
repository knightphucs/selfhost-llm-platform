using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.Routing;

/// <param name="Priority">Nhỏ hơn được thử trước — các route cùng virtual model tạo thành chuỗi fallback.</param>
public sealed record CreateRouteCommand(Guid TenantId, Guid VirtualModelId, Guid DeploymentId, int Priority, int Weight = 1)
    : IRequest<Result<Route>>, ITenantRequest;

public sealed record UpdateRouteCommand(Guid TenantId, Guid RouteId, int Priority, int Weight, bool Enabled)
    : IRequest<Result<Route>>, ITenantRequest;

public sealed record DeleteRouteCommand(Guid TenantId, Guid RouteId) : IRequest<Result>, ITenantRequest;

/// <summary>Các route của một virtual model, theo thứ tự fallback.</summary>
public sealed record ListRoutesQuery(Guid TenantId, Guid VirtualModelId) : IRequest<Result<IReadOnlyList<Route>>>, ITenantRequest;

internal sealed class CreateRouteValidator : AbstractValidator<CreateRouteCommand>
{
    public CreateRouteValidator()
    {
        RuleFor(c => c.VirtualModelId).NotEmpty();
        RuleFor(c => c.DeploymentId).NotEmpty();
        RuleFor(c => c.Priority).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Weight).GreaterThanOrEqualTo(1);
    }
}

internal sealed class UpdateRouteValidator : AbstractValidator<UpdateRouteCommand>
{
    public UpdateRouteValidator()
    {
        RuleFor(c => c.RouteId).NotEmpty();
        RuleFor(c => c.Priority).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Weight).GreaterThanOrEqualTo(1);
    }
}

internal sealed class CreateRouteHandler(
    ITenantRepository<Route> routes,
    ITenantRepository<VirtualModel> virtualModels,
    ITenantRepository<Deployment> deployments,
    IAuditTrail audit,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateRouteCommand, Result<Route>>
{
    public async Task<Result<Route>> HandleAsync(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        if (!await virtualModels.ExistsAsync(request.TenantId, v => v.Id == request.VirtualModelId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<VirtualModel>(request.VirtualModelId);
        }

        if (!await deployments.ExistsAsync(request.TenantId, d => d.Id == request.DeploymentId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<Deployment>(request.DeploymentId);
        }

        if (await routes.ExistsAsync(
                request.TenantId,
                r => r.VirtualModelId == request.VirtualModelId && r.DeploymentId == request.DeploymentId,
                cancellationToken))
        {
            return Error.Conflict("route.duplicate", "Virtual model đã có route tới deployment này.");
        }

        var created = Route.Create(request.TenantId, request.VirtualModelId, request.DeploymentId, request.Priority, request.Weight);
        if (created.IsFailure)
        {
            return created;
        }

        var route = created.Value;
        routes.Add(route);
        audit.Record(request.TenantId, AuditAction.Create, nameof(Route), route.Id.ToString(), null, AuditSnapshot.Of(route));
        return await unitOfWork.SaveAndReturnAsync(route, cancellationToken);
    }
}

internal sealed class UpdateRouteHandler(ITenantRepository<Route> routes, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRouteCommand, Result<Route>>
{
    public async Task<Result<Route>> HandleAsync(UpdateRouteCommand request, CancellationToken cancellationToken)
    {
        if (await routes.GetAsync(request.TenantId, request.RouteId, cancellationToken) is not { } route)
        {
            return UseCaseExtensions.NotFound<Route>(request.RouteId);
        }

        var before = AuditSnapshot.Of(route);
        var updated = route.Update(request.Priority, request.Weight);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        if (request.Enabled)
        {
            route.Enable();
        }
        else
        {
            route.Disable();
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(Route), route.Id.ToString(), before, AuditSnapshot.Of(route));
        return await unitOfWork.SaveAndReturnAsync(route, cancellationToken);
    }
}

internal sealed class DeleteRouteHandler(ITenantRepository<Route> routes, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteRouteCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        if (await routes.GetAsync(request.TenantId, request.RouteId, cancellationToken) is not { } route)
        {
            return UseCaseExtensions.NotFound<Route>(request.RouteId);
        }

        routes.Remove(route);
        audit.Record(request.TenantId, AuditAction.Delete, nameof(Route), route.Id.ToString(), AuditSnapshot.Of(route), null);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class ListRoutesHandler(ITenantRepository<Route> routes) : IRequestHandler<ListRoutesQuery, Result<IReadOnlyList<Route>>>
{
    public async Task<Result<IReadOnlyList<Route>>> HandleAsync(ListRoutesQuery request, CancellationToken cancellationToken)
    {
        var list = await routes.ListAsync(request.TenantId, r => r.VirtualModelId == request.VirtualModelId, cancellationToken);
        return Result<IReadOnlyList<Route>>.Success(list.OrderBy(r => r.Priority).ThenByDescending(r => r.Weight).ThenBy(r => r.DeploymentId).ToList());
    }
}
