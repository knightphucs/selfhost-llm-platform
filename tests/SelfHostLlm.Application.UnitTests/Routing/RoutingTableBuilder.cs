using SelfHostLlm.Application.Routing;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.UnitTests.Routing;

/// <summary>Dựng <see cref="RoutingTable"/> cho test một cách đọc được.</summary>
internal sealed class RoutingTableBuilder
{
    private readonly List<ModelEntry> _models = [];
    private readonly List<DeploymentEntry> _deployments = [];
    private readonly List<VirtualModelEntry> _virtualModels = [];
    private readonly List<RouteEntry> _routes = [];
    private int _port = 11000;

    public ModelEntry Model(Guid tenantId, string name, params ModelCapability[] capabilities)
    {
        var model = new ModelEntry(tenantId, Guid.NewGuid(), name,
            (capabilities.Length == 0 ? [ModelCapability.Chat] : capabilities).ToHashSet());
        _models.Add(model);
        return model;
    }

    public DeploymentEntry Deployment(
        ModelEntry model,
        HealthStatus health = HealthStatus.Healthy,
        bool enabled = true,
        int? latencyMs = null,
        Guid? tenantId = null)
    {
        var deployment = new DeploymentEntry(
            tenantId ?? model.TenantId, Guid.NewGuid(), model.Id, ProviderKind.Ollama,
            new Uri($"http://192.168.1.50:{_port++}"), "qwen2.5:7b", health, enabled, latencyMs);
        _deployments.Add(deployment);
        return deployment;
    }

    public VirtualModelEntry VirtualModel(Guid tenantId, string name, TaskKind task = TaskKind.Chat)
    {
        var virtualModel = new VirtualModelEntry(tenantId, Guid.NewGuid(), name, task);
        _virtualModels.Add(virtualModel);
        return virtualModel;
    }

    public RoutingTableBuilder Route(VirtualModelEntry virtualModel, DeploymentEntry deployment, int priority, int weight = 1, bool enabled = true)
    {
        _routes.Add(new RouteEntry(virtualModel.TenantId, virtualModel.Id, deployment.Id, priority, weight, enabled));
        return this;
    }

    public RoutingTable Build() => new(_models, _deployments, _virtualModels, _routes);
}
