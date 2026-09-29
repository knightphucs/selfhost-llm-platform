namespace SelfHostLlm.Contracts.Internal;

/// <summary>
/// Hợp đồng Control plane → Gateway (<c>GET /internal/config-snapshot</c>). Gateway chỉ đọc
/// cấu hình qua snapshot này, không query bảng config (bất biến 2), và tiếp tục phục vụ bằng
/// snapshot gần nhất khi control plane không liên lạc được (bất biến 1).
/// </summary>
public sealed record ConfigSnapshotDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SnapshotModelDto> Models,
    IReadOnlyList<SnapshotDeploymentDto> Deployments,
    IReadOnlyList<SnapshotVirtualModelDto> VirtualModels,
    IReadOnlyList<SnapshotRouteDto> Routes,
    IReadOnlyList<SnapshotApiKeyDto> ApiKeys,
    IReadOnlyList<SnapshotQuotaDto> Quotas,
    IReadOnlyList<SnapshotMonthlyUsageDto> MonthlyUsage)
{
    public const string InternalTokenHeader = "X-Internal-Token";
}

public sealed record SnapshotModelDto(Guid TenantId, Guid Id, string Name, IReadOnlyList<string> Capabilities);

/// <param name="ApiKeyEncrypted">Ciphertext Data Protection — Gateway giải mã bằng key ring dùng chung; không bao giờ là plaintext.</param>
public sealed record SnapshotDeploymentDto(
    Guid TenantId,
    Guid Id,
    Guid ModelId,
    string ProviderKind,
    string BaseUrl,
    string RemoteModelName,
    string HealthStatus,
    bool Enabled,
    int? LatencyMsP50,
    string? ApiKeyEncrypted);

public sealed record SnapshotVirtualModelDto(Guid TenantId, Guid Id, string Name, string Task);

public sealed record SnapshotRouteDto(Guid TenantId, Guid VirtualModelId, Guid DeploymentId, int Priority, int Weight, bool Enabled);

/// <summary>Chỉ key còn hiệu lực của consumer đang bật. Chỉ có hash — không có plaintext.</summary>
public sealed record SnapshotApiKeyDto(Guid TenantId, Guid Id, Guid ConsumerId, string KeyHash, string KeyPrefix, DateTimeOffset? ExpiresAt);

public sealed record SnapshotQuotaDto(Guid TenantId, Guid ConsumerId, int? TokensPerMinute, long? TokensPerMonth, int? MaxConcurrentRequests);

public sealed record SnapshotMonthlyUsageDto(Guid ConsumerId, long Tokens, DateTimeOffset AsOf);
