using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.ControlPlane.Api.Http;
using SelfHostLlm.ControlPlane.Api.Mapping;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

/// <summary>Virtual model và route — cấu hình task routing + chuỗi fallback.</summary>
internal static class RoutingEndpoints
{
    public static IEndpointRouteBuilder MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var virtualModels = app.MapGroup(Routes.Tenant + "/virtual-models").WithTags("Routing");

        virtualModels.MapGet("/", async (Guid tenantId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListVirtualModelsQuery(tenantId), ct)).ToOk(list => list.Select(v => v.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.RoutesRead);

        virtualModels.MapGet("/{virtualModelId:guid}", async (Guid tenantId, Guid virtualModelId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetVirtualModelQuery(tenantId, virtualModelId), ct)).ToOk(v => v.ToResponse()))
            .RequireAuthorization(Permissions.RoutesRead);

        virtualModels.MapPost("/", async (Guid tenantId, CreateVirtualModelRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateVirtualModelCommand(tenantId, b.Name, b.Task, b.Description), ct))
                .ToCreated(v => v.ToResponse(), v => $"/api/v1/tenants/{tenantId}/virtual-models/{v.Id}"))
            .RequireAuthorization(Permissions.RoutesWrite);

        virtualModels.MapPut("/{virtualModelId:guid}", async (Guid tenantId, Guid virtualModelId, UpdateVirtualModelRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpdateVirtualModelCommand(tenantId, virtualModelId, b.Name, b.Task, b.Description), ct)).ToOk(v => v.ToResponse()))
            .RequireAuthorization(Permissions.RoutesWrite);

        virtualModels.MapDelete("/{virtualModelId:guid}", async (Guid tenantId, Guid virtualModelId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new DeleteVirtualModelCommand(tenantId, virtualModelId), ct)).ToNoContent())
            .RequireAuthorization(Permissions.RoutesWrite);

        virtualModels.MapGet("/{virtualModelId:guid}/routes", async (Guid tenantId, Guid virtualModelId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListRoutesQuery(tenantId, virtualModelId), ct)).ToOk(list => list.Select(r => r.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.RoutesRead);

        virtualModels.MapPost("/{virtualModelId:guid}/routes", async (Guid tenantId, Guid virtualModelId, CreateRouteRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateRouteCommand(tenantId, virtualModelId, b.DeploymentId, b.Priority, b.Weight), ct))
                .ToCreated(r => r.ToResponse(), r => $"/api/v1/tenants/{tenantId}/routes/{r.Id}"))
            .RequireAuthorization(Permissions.RoutesWrite);

        var routes = app.MapGroup(Routes.Tenant + "/routes").WithTags("Routing");

        routes.MapPut("/{routeId:guid}", async (Guid tenantId, Guid routeId, UpdateRouteRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpdateRouteCommand(tenantId, routeId, b.Priority, b.Weight, b.Enabled), ct)).ToOk(r => r.ToResponse()))
            .RequireAuthorization(Permissions.RoutesWrite);

        routes.MapDelete("/{routeId:guid}", async (Guid tenantId, Guid routeId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new DeleteRouteCommand(tenantId, routeId), ct)).ToNoContent())
            .RequireAuthorization(Permissions.RoutesWrite);

        return app;
    }
}
