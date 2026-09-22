using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.Routing;

/// <summary>
/// Read model in-memory mà Gateway dựng từ <c>ConfigSnapshot</c> (bất biến 2: Gateway không
/// query bảng config). Bất biến sau khi tạo — snapshot mới thì dựng bảng mới và thay nguyên khối.
/// </summary>
public sealed class RoutingTable
{
    private readonly Dictionary<Guid, ModelEntry> _models;
    private readonly Dictionary<(Guid TenantId, string Name), ModelEntry> _modelsByName;
    private readonly Dictionary<Guid, DeploymentEntry> _deployments;
    private readonly ILookup<Guid, DeploymentEntry> _deploymentsByModel;
    private readonly Dictionary<(Guid TenantId, string Name), VirtualModelEntry> _virtualModelsByName;
    private readonly ILookup<(Guid TenantId, TaskKind Task), VirtualModelEntry> _virtualModelsByTask;
    private readonly ILookup<Guid, RouteEntry> _routesByVirtualModel;

    public RoutingTable(
        IEnumerable<ModelEntry> models,
        IEnumerable<DeploymentEntry> deployments,
        IEnumerable<VirtualModelEntry> virtualModels,
        IEnumerable<RouteEntry> routes)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(virtualModels);
        ArgumentNullException.ThrowIfNull(routes);

        var modelList = models.ToList();
        var deploymentList = deployments.ToList();
        var virtualModelList = virtualModels.ToList();

        _models = modelList.ToDictionary(m => m.Id);
        _modelsByName = modelList.ToDictionary(m => (m.TenantId, m.Name));
        _deployments = deploymentList.ToDictionary(d => d.Id);
        _deploymentsByModel = deploymentList.ToLookup(d => d.ModelId);
        _virtualModelsByName = virtualModelList.ToDictionary(v => (v.TenantId, v.Name));
        _virtualModelsByTask = virtualModelList
            .OrderBy(v => v.Name, StringComparer.Ordinal)
            .ToLookup(v => (v.TenantId, v.Task));
        _routesByVirtualModel = routes.ToLookup(r => r.VirtualModelId);
    }

    public static RoutingTable Empty { get; } = new([], [], [], []);

    internal ModelEntry? FindModel(Guid tenantId, string name) => _modelsByName.GetValueOrDefault((tenantId, name));

    internal ModelEntry? GetModel(Guid modelId) => _models.GetValueOrDefault(modelId);

    internal DeploymentEntry? GetDeployment(Guid deploymentId) => _deployments.GetValueOrDefault(deploymentId);

    internal IEnumerable<DeploymentEntry> DeploymentsOf(Guid modelId) => _deploymentsByModel[modelId];

    internal VirtualModelEntry? FindVirtualModel(Guid tenantId, string name) =>
        _virtualModelsByName.GetValueOrDefault((tenantId, name));

    /// <summary>Virtual model đầu tiên (theo tên) phục vụ <paramref name="task"/> trong tenant.</summary>
    internal VirtualModelEntry? FindVirtualModelForTask(Guid tenantId, TaskKind task) =>
        _virtualModelsByTask[(tenantId, task)].FirstOrDefault();

    internal IEnumerable<RouteEntry> RoutesOf(Guid virtualModelId) => _routesByVirtualModel[virtualModelId];
}

public sealed record ModelEntry(Guid TenantId, Guid Id, string Name, IReadOnlySet<ModelCapability> Capabilities);

public sealed record DeploymentEntry(
    Guid TenantId,
    Guid Id,
    Guid ModelId,
    ProviderKind ProviderKind,
    Uri BaseUrl,
    string RemoteModelName,
    HealthStatus Health,
    bool Enabled,
    int? LatencyMsP50)
{
    /// <summary>Cùng luật với <c>Deployment.IsRoutable</c> ở Domain: enabled và không Unhealthy.</summary>
    public bool IsRoutable => Enabled && Health != HealthStatus.Unhealthy;
}

public sealed record VirtualModelEntry(Guid TenantId, Guid Id, string Name, TaskKind Task);

public sealed record RouteEntry(Guid TenantId, Guid VirtualModelId, Guid DeploymentId, int Priority, int Weight, bool Enabled);
