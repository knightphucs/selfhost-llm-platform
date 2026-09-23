using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Deployments;
using SelfHostLlm.Application.Models;
using SelfHostLlm.Application.Providers;
using SelfHostLlm.ControlPlane.Api.Http;
using SelfHostLlm.ControlPlane.Api.Mapping;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

/// <summary>Model, Provider, Deployment — mô hình Model × Provider × Address.</summary>
internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        MapModels(app.MapGroup(Routes.Tenant + "/models").WithTags("Models"));
        MapProviders(app.MapGroup(Routes.Tenant + "/providers").WithTags("Providers"));
        MapDeployments(app.MapGroup(Routes.Tenant + "/deployments").WithTags("Deployments"));
        return app;
    }

    private static void MapModels(RouteGroupBuilder group)
    {
        group.MapGet("/", async (Guid tenantId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListModelsQuery(tenantId), ct)).ToOk(list => list.Select(m => m.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.ModelsRead);

        group.MapGet("/{modelId:guid}", async (Guid tenantId, Guid modelId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetModelQuery(tenantId, modelId), ct)).ToOk(m => m.ToResponse()))
            .RequireAuthorization(Permissions.ModelsRead);

        group.MapPost("/", async (Guid tenantId, CreateModelRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateModelCommand(tenantId, b.Name, b.Family, b.ParamSize, b.Quantization, b.ContextLength, b.Capabilities ?? [], b.TaskTags ?? []), ct))
                .ToCreated(m => m.ToResponse(), m => $"/api/v1/tenants/{tenantId}/models/{m.Id}"))
            .RequireAuthorization(Permissions.ModelsWrite);

        group.MapPut("/{modelId:guid}", async (Guid tenantId, Guid modelId, UpdateModelRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpdateModelCommand(tenantId, modelId, b.Name, b.Family, b.ParamSize, b.Quantization, b.ContextLength, b.Capabilities ?? [], b.TaskTags ?? []), ct))
                .ToOk(m => m.ToResponse()))
            .RequireAuthorization(Permissions.ModelsWrite);

        group.MapDelete("/{modelId:guid}", async (Guid tenantId, Guid modelId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new DeleteModelCommand(tenantId, modelId), ct)).ToNoContent())
            .RequireAuthorization(Permissions.ModelsWrite);
    }

    private static void MapProviders(RouteGroupBuilder group)
    {
        group.MapGet("/", async (Guid tenantId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListProvidersQuery(tenantId), ct)).ToOk(list => list.Select(p => p.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.ProvidersRead);

        group.MapGet("/{providerId:guid}", async (Guid tenantId, Guid providerId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetProviderQuery(tenantId, providerId), ct)).ToOk(p => p.ToResponse()))
            .RequireAuthorization(Permissions.ProvidersRead);

        group.MapPost("/", async (Guid tenantId, CreateProviderRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateProviderCommand(tenantId, b.Name, b.Kind, b.Description), ct))
                .ToCreated(p => p.ToResponse(), p => $"/api/v1/tenants/{tenantId}/providers/{p.Id}"))
            .RequireAuthorization(Permissions.ProvidersWrite);

        group.MapPut("/{providerId:guid}", async (Guid tenantId, Guid providerId, UpdateProviderRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpdateProviderCommand(tenantId, providerId, b.Name, b.Kind, b.Description), ct)).ToOk(p => p.ToResponse()))
            .RequireAuthorization(Permissions.ProvidersWrite);

        group.MapDelete("/{providerId:guid}", async (Guid tenantId, Guid providerId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new DeleteProviderCommand(tenantId, providerId), ct)).ToNoContent())
            .RequireAuthorization(Permissions.ProvidersWrite);
    }

    private static void MapDeployments(RouteGroupBuilder group)
    {
        group.MapGet("/", async (Guid tenantId, Guid? modelId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListDeploymentsQuery(tenantId, modelId), ct)).ToOk(list => list.Select(x => x.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.DeploymentsRead);

        group.MapGet("/{deploymentId:guid}", async (Guid tenantId, Guid deploymentId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetDeploymentQuery(tenantId, deploymentId), ct)).ToOk(x => x.ToResponse()))
            .RequireAuthorization(Permissions.DeploymentsRead);

        group.MapPost("/", async (Guid tenantId, CreateDeploymentRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateDeploymentCommand(tenantId, b.ModelId, b.ModelVersionId, b.ProviderId, b.BaseUrl, b.RemoteModelName, b.ApiKey), ct))
                .ToCreated(x => x.ToResponse(), x => $"/api/v1/tenants/{tenantId}/deployments/{x.Id}"))
            .RequireAuthorization(Permissions.DeploymentsWrite);

        group.MapPut("/{deploymentId:guid}", async (Guid tenantId, Guid deploymentId, UpdateDeploymentRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpdateDeploymentEndpointCommand(tenantId, deploymentId, b.BaseUrl, b.RemoteModelName), ct)).ToOk(x => x.ToResponse()))
            .RequireAuthorization(Permissions.DeploymentsWrite);

        group.MapPut("/{deploymentId:guid}/api-key", async (Guid tenantId, Guid deploymentId, SetDeploymentApiKeyRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new SetDeploymentApiKeyCommand(tenantId, deploymentId, b.ApiKey), ct)).ToOk(x => x.ToResponse()))
            .RequireAuthorization(Permissions.DeploymentsWrite);

        group.MapPost("/{deploymentId:guid}/probe", async (Guid tenantId, Guid deploymentId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ProbeDeploymentCommand(tenantId, deploymentId), ct))
                .ToOk(p => new DeploymentProbeResponse(p.Probe.Healthy, p.Probe.LatencyMs, p.Probe.StatusCode, p.Probe.Error, p.Deployment.ToResponse())))
            .RequireAuthorization(Permissions.DeploymentsWrite);

        group.MapPost("/{deploymentId:guid}/enable", async (Guid tenantId, Guid deploymentId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new SetDeploymentEnabledCommand(tenantId, deploymentId, true), ct)).ToOk(x => x.ToResponse()))
            .RequireAuthorization(Permissions.DeploymentsWrite);

        group.MapPost("/{deploymentId:guid}/disable", async (Guid tenantId, Guid deploymentId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new SetDeploymentEnabledCommand(tenantId, deploymentId, false), ct)).ToOk(x => x.ToResponse()))
            .RequireAuthorization(Permissions.DeploymentsWrite);
    }
}
