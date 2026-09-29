namespace SelfHostLlm.Contracts.Admin;

/// <param name="Task">Giá trị: <c>Chat</c>, <c>Coding</c>, <c>Embedding</c>, <c>Summarization</c>, <c>Classification</c>.</param>
public sealed record CreateVirtualModelRequest(string Name, string Task, string? Description);

public sealed record UpdateVirtualModelRequest(string Name, string Task, string? Description);

public sealed record VirtualModelResponse(Guid Id, string Name, string Task, string? Description);

/// <param name="Priority">Nhỏ hơn được thử trước; các route cùng virtual model tạo thành chuỗi fallback.</param>
public sealed record CreateRouteRequest(Guid VirtualModelId, Guid DeploymentId, int Priority, int Weight = 1);

public sealed record UpdateRouteRequest(int Priority, int Weight, bool Enabled);

public sealed record RouteResponse(Guid Id, Guid VirtualModelId, Guid DeploymentId, int Priority, int Weight, bool Enabled);
