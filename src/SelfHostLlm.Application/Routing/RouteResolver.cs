using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.Routing;

/// <param name="Model">Field <c>model</c> client gửi: virtual model (<c>code-fast</c>) hoặc tên model thật.</param>
/// <param name="Task">Field mở rộng <c>task</c> cho explicit task routing (không phân biệt hoa thường).</param>
/// <param name="RequiredCapability">Chat cho <c>/v1/chat/completions</c>, Embedding cho <c>/v1/embeddings</c>.</param>
public sealed record RouteRequest(Guid TenantId, string? Model, string? Task, ModelCapability RequiredCapability);

/// <summary>Kết quả định tuyến: danh sách deployment <b>có thứ tự</b> — phần tử sau là fallback của phần tử trước.</summary>
public sealed record RoutePlan(
    string? RequestedModel,
    TaskKind? Task,
    Guid? VirtualModelId,
    IReadOnlyList<DeploymentEntry> Candidates);

/// <summary>
/// Chọn chuỗi deployment cho một request. Chỉ nhìn các entry cùng tenant với request — virtual
/// model trùng tên của tenant khác không bao giờ được chọn.
/// </summary>
public static class RouteResolver
{
    public static Result<RoutePlan> Resolve(RoutingTable table, RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(request);

        TaskKind? task = null;
        if (!string.IsNullOrWhiteSpace(request.Task))
        {
            if (!TryParseTask(request.Task, out var parsed))
            {
                return Error.Validation("routing.task.invalid", $"task không hợp lệ. Giá trị hợp lệ: {string.Join(", ", Enum.GetNames<TaskKind>())}.");
            }

            task = parsed;
        }

        var model = request.Model?.Trim();
        if (!string.IsNullOrEmpty(model))
        {
            // 1. Virtual model của tenant.
            if (table.FindVirtualModel(request.TenantId, model) is { } virtualModel)
            {
                return FromVirtualModel(table, request, virtualModel, model);
            }

            // 2. Tên model thật của tenant.
            if (table.FindModel(request.TenantId, model) is { } directModel)
            {
                return FromModel(table, request, directModel, model, task);
            }
        }

        // 3. Explicit task routing: model không khớp (hoặc "auto") nhưng có task.
        if (task is { } requestedTask)
        {
            return table.FindVirtualModelForTask(request.TenantId, requestedTask) is { } taskModel
                ? FromVirtualModel(table, request, taskModel, model)
                : Error.NotFound("routing.task_not_configured", $"Chưa cấu hình virtual model cho task {requestedTask}.");
        }

        return Error.NotFound("routing.model_not_found", $"Không tìm thấy model '{model}'.");
    }

    private static Result<RoutePlan> FromVirtualModel(
        RoutingTable table,
        RouteRequest request,
        VirtualModelEntry virtualModel,
        string? requestedModel)
    {
        var linked = table.RoutesOf(virtualModel.Id)
            .Where(r => r.Enabled && r.TenantId == request.TenantId)
            .Select(r => (Route: r, Deployment: table.GetDeployment(r.DeploymentId)))
            .Where(x => x.Deployment is not null && x.Deployment.TenantId == request.TenantId)
            .Select(x => (x.Route, Deployment: x.Deployment!))
            .ToList();

        var capable = linked.Where(x => Supports(table, x.Deployment, request.RequiredCapability)).ToList();
        if (linked.Count > 0 && capable.Count == 0)
        {
            return CapabilityMismatch(virtualModel.Name, request.RequiredCapability);
        }

        var candidates = capable
            .Where(x => x.Deployment.IsRoutable)
            .OrderBy(x => x.Route.Priority)
            .ThenByDescending(x => x.Route.Weight)
            .ThenBy(x => x.Deployment.Id)
            .Select(x => x.Deployment)
            .ToList();

        return candidates.Count == 0
            ? NoAvailableDeployment(virtualModel.Name)
            : new RoutePlan(requestedModel, virtualModel.Task, virtualModel.Id, candidates);
    }

    private static Result<RoutePlan> FromModel(
        RoutingTable table,
        RouteRequest request,
        ModelEntry model,
        string requestedModel,
        TaskKind? task)
    {
        if (!model.Capabilities.Contains(request.RequiredCapability))
        {
            return CapabilityMismatch(model.Name, request.RequiredCapability);
        }

        // Không có priority khi gọi thẳng model: Healthy trước Unknown, rồi latency thấp trước.
        var candidates = table.DeploymentsOf(model.Id)
            .Where(d => d.TenantId == request.TenantId && d.IsRoutable)
            .OrderBy(d => d.Health == HealthStatus.Healthy ? 0 : 1)
            .ThenBy(d => d.LatencyMsP50 ?? int.MaxValue)
            .ThenBy(d => d.Id)
            .ToList();

        return candidates.Count == 0
            ? NoAvailableDeployment(model.Name)
            : new RoutePlan(requestedModel, task, null, candidates);
    }

    private static bool Supports(RoutingTable table, DeploymentEntry deployment, ModelCapability capability) =>
        table.GetModel(deployment.ModelId)?.Capabilities.Contains(capability) == true;

    private static bool TryParseTask(string value, out TaskKind task)
    {
        // Không dùng Enum.TryParse trực tiếp: nó nhận cả chuỗi số ("1").
        var name = Enum.GetNames<TaskKind>().FirstOrDefault(n => n.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        task = name is null ? default : Enum.Parse<TaskKind>(name);
        return name is not null;
    }

    private static Error CapabilityMismatch(string name, ModelCapability capability) =>
        Error.Validation("routing.capability_mismatch", $"'{name}' không hỗ trợ {capability}.");

    private static Error NoAvailableDeployment(string name) =>
        Error.Unavailable("routing.no_available_deployment", $"Không còn deployment khả dụng cho '{name}'.");
}
