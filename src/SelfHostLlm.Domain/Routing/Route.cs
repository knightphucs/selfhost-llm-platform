using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Routing;

/// <summary>
/// Ánh xạ <see cref="VirtualModel"/> → Deployment. <see cref="Priority"/> nhỏ hơn được thử
/// trước; các route cùng virtual model tạo thành chuỗi fallback.
/// </summary>
public sealed class Route : Entity<Guid>, ITenantScoped
{
    private Route()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid VirtualModelId { get; private set; }

    public Guid DeploymentId { get; private set; }

    public int Priority { get; private set; }

    /// <summary>Trọng số chia tải giữa các route cùng priority (dự phòng cho mở rộng).</summary>
    public int Weight { get; private set; }

    public bool Enabled { get; private set; }

    public static Result<Route> Create(Guid tenantId, Guid virtualModelId, Guid deploymentId, int priority, int weight = 1)
    {
        var route = new Route
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VirtualModelId = virtualModelId,
            DeploymentId = deploymentId,
            Enabled = true,
        };

        var error = Guard.First(
            Guard.NotEmpty(tenantId, "route.tenant_id"),
            Guard.NotEmpty(virtualModelId, "route.virtual_model_id"),
            Guard.NotEmpty(deploymentId, "route.deployment_id"))
            ?? route.Apply(priority, weight);
        return error is null ? route : error;
    }

    public Result Update(int priority, int weight)
    {
        var error = Apply(priority, weight);
        return error is null ? Result.Success() : error;
    }

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    private Error? Apply(int priority, int weight)
    {
        var error = Guard.First(
            Guard.NonNegative(priority, "route.priority"),
            Guard.Positive(weight, "route.weight"));
        if (error is not null)
        {
            return error;
        }

        Priority = priority;
        Weight = weight;
        return null;
    }
}
