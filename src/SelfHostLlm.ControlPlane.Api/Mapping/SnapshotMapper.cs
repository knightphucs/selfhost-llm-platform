using System.Security.Cryptography;
using System.Text.Json;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Contracts.Internal;

namespace SelfHostLlm.ControlPlane.Api.Mapping;

internal static class SnapshotMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ConfigSnapshotDto ToDto(this ConfigSnapshotData data)
    {
        var providerKinds = data.Providers.ToDictionary(p => p.Id, p => p.Kind.ToString());

        return new ConfigSnapshotDto(
            data.GeneratedAt,
            data.Models
                .Select(m => new SnapshotModelDto(m.TenantId, m.Id, m.Name, m.Capabilities.Select(c => c.ToString()).ToList()))
                .ToList(),
            data.Deployments
                .Select(d => new SnapshotDeploymentDto(
                    d.TenantId, d.Id, d.ModelId, providerKinds.GetValueOrDefault(d.ProviderId, "OpenAiCompatible"),
                    d.Address.ToString(), d.RemoteModelName, d.HealthStatus.ToString(), d.Enabled, d.LatencyMsP50,
                    d.ApiKeyEncrypted))
                .ToList(),
            data.VirtualModels.Select(v => new SnapshotVirtualModelDto(v.TenantId, v.Id, v.Name, v.Task.ToString())).ToList(),
            data.Routes
                .Select(r => new SnapshotRouteDto(r.TenantId, r.VirtualModelId, r.DeploymentId, r.Priority, r.Weight, r.Enabled))
                .ToList(),
            data.ApiKeys
                .Select(k => new SnapshotApiKeyDto(k.TenantId, k.Id, k.ConsumerId, k.KeyHash, k.KeyPrefix, k.ExpiresAt))
                .ToList(),
            data.Quotas
                .Select(q => new SnapshotQuotaDto(q.TenantId, q.ConsumerId, q.TokensPerMinute, q.TokensPerMonth, q.MaxConcurrentRequests))
                .ToList(),
            data.MonthlyUsage.Select(u => new SnapshotMonthlyUsageDto(u.ConsumerId, u.Tokens, data.GeneratedAt)).ToList());
    }

    /// <summary>
    /// ETag theo NỘI DUNG: bỏ qua thời điểm tạo (<c>GeneratedAt</c>, <c>AsOf</c>) để cấu hình
    /// không đổi thì ETag không đổi và Gateway nhận 304.
    /// </summary>
    public static string ComputeETag(ConfigSnapshotDto snapshot)
    {
        var stable = snapshot with
        {
            GeneratedAt = default,
            MonthlyUsage = snapshot.MonthlyUsage.Select(u => u with { AsOf = default }).ToList(),
        };
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(stable, JsonOptions));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }
}
