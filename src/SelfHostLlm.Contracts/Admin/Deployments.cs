using SelfHostLlm.Contracts.Admin.Common;

namespace SelfHostLlm.Contracts.Admin;

/// <param name="BaseUrl">Address trong LAN, ví dụ <c>http://192.168.1.50:11434</c>.</param>
/// <param name="ApiKey">API key phía engine (nếu có). Control plane mã hoá trước khi lưu; không bao giờ trả lại.</param>
public sealed record CreateDeploymentRequest(
    Guid ModelId,
    Guid? ModelVersionId,
    Guid ProviderId,
    string BaseUrl,
    string RemoteModelName,
    string? ApiKey)
{
    public override string ToString() =>
        $"{nameof(CreateDeploymentRequest)} {{ ModelId = {ModelId}, ProviderId = {ProviderId}, BaseUrl = {BaseUrl}, " +
        $"RemoteModelName = {RemoteModelName}, ApiKey = {Secret.Mask(ApiKey)} }}";
}

public sealed record UpdateDeploymentRequest(string BaseUrl, string RemoteModelName);

/// <param name="ApiKey">Key mới; <c>null</c> để xoá key.</param>
public sealed record SetDeploymentApiKeyRequest(string? ApiKey)
{
    public override string ToString() => $"{nameof(SetDeploymentApiKeyRequest)} {{ ApiKey = {Secret.Mask(ApiKey)} }}";
}

/// <param name="HasApiKey">Chỉ cho biết có key hay không — không bao giờ trả key.</param>
/// <param name="HealthStatus">Giá trị: <c>Unknown</c>, <c>Healthy</c>, <c>Unhealthy</c>.</param>
public sealed record DeploymentResponse(
    Guid Id,
    Guid ModelId,
    Guid? ModelVersionId,
    Guid ProviderId,
    string BaseUrl,
    string RemoteModelName,
    bool HasApiKey,
    string HealthStatus,
    int ConsecutiveFailures,
    int? LatencyMsP50,
    DateTimeOffset? LastProbedAt,
    bool Enabled);

/// <summary>Kết quả probe thủ công (<c>POST .../deployments/{id}/probe</c>).</summary>
public sealed record DeploymentProbeResponse(bool Healthy, int LatencyMs, int? StatusCode, string? Error, DeploymentResponse Deployment);
