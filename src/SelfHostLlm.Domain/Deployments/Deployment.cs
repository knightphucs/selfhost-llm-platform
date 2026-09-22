using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Deployments;

/// <summary>
/// Cầu nối Model × Provider tại một <see cref="Address"/>. Là đích thật mà gateway gọi tới.
/// Không xoá cứng — dùng <see cref="Disable"/> để giữ toàn vẹn usage lịch sử.
/// </summary>
public sealed class Deployment : Entity<Guid>, ITenantScoped
{
    private Deployment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ModelId { get; private set; }

    /// <summary>Null khi deploy model gốc chưa fine-tune.</summary>
    public Guid? ModelVersionId { get; private set; }

    public Guid ProviderId { get; private set; }

    public Address Address { get; private set; } = null!;

    /// <summary>Ciphertext Data Protection của API key phía engine (QĐ-8). Domain không giải mã.</summary>
    public string? ApiKeyEncrypted { get; private set; }

    /// <summary>Tên model phía engine, ví dụ <c>qwen2.5:7b-instruct-q4_K_M</c>.</summary>
    public string RemoteModelName { get; private set; } = null!;

    public HealthStatus HealthStatus { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    /// <summary>Latency p50 do Worker.Health tính trên cửa sổ probe gần nhất.</summary>
    public int? LatencyMsP50 { get; private set; }

    public DateTimeOffset? LastProbedAt { get; private set; }

    public bool Enabled { get; private set; }

    /// <summary>Có được đưa vào danh sách ứng viên của RouteResolver hay không.</summary>
    public bool IsRoutable => Enabled && HealthStatus != HealthStatus.Unhealthy;

    public static Result<Deployment> Create(
        Guid tenantId,
        Guid modelId,
        Guid? modelVersionId,
        Guid providerId,
        Address address,
        string remoteModelName,
        string? apiKeyEncrypted)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "deployment.tenant_id"),
            Guard.NotEmpty(modelId, "deployment.model_id"),
            Guard.NotEmpty(providerId, "deployment.provider_id"),
            Guard.NotBlank(remoteModelName, "deployment.remote_model_name"));
        if (error is not null)
        {
            return error;
        }

        ArgumentNullException.ThrowIfNull(address);

        return new Deployment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModelId = modelId,
            ModelVersionId = modelVersionId,
            ProviderId = providerId,
            Address = address,
            RemoteModelName = remoteModelName.Trim(),
            ApiKeyEncrypted = apiKeyEncrypted,
            HealthStatus = HealthStatus.Unknown,
            Enabled = true,
        };
    }

    public Result UpdateEndpoint(Address address, string remoteModelName)
    {
        ArgumentNullException.ThrowIfNull(address);
        var error = Guard.NotBlank(remoteModelName, "deployment.remote_model_name");
        if (error is not null)
        {
            return error;
        }

        Address = address;
        RemoteModelName = remoteModelName.Trim();
        return Result.Success();
    }

    /// <summary>Đặt lại API key đã mã hoá; null để xoá.</summary>
    public void SetApiKeyEncrypted(string? apiKeyEncrypted) => ApiKeyEncrypted = apiKeyEncrypted;

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    public void RecordProbeSuccess(int latencyMsP50, DateTimeOffset now)
    {
        HealthStatus = HealthStatus.Healthy;
        ConsecutiveFailures = 0;
        LatencyMsP50 = Math.Max(0, latencyMsP50);
        LastProbedAt = now.ToUniversalTime();
    }

    /// <summary>
    /// Ghi nhận một lần probe lỗi. Chỉ chuyển Unhealthy khi số lần lỗi liên tiếp đạt
    /// <paramref name="unhealthyThreshold"/> — tránh nhấp nháy khi mạng LAN chập chờn.
    /// </summary>
    public void RecordProbeFailure(DateTimeOffset now, int unhealthyThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(unhealthyThreshold, 1);

        ConsecutiveFailures++;
        LastProbedAt = now.ToUniversalTime();
        if (ConsecutiveFailures >= unhealthyThreshold)
        {
            HealthStatus = HealthStatus.Unhealthy;
        }
    }
}
