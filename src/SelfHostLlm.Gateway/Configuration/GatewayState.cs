using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Contracts.Internal;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Gateway.Configuration;

/// <summary>ApiKey trong bảng tra của Gateway — chỉ có hash làm khoá, không có plaintext.</summary>
internal sealed record ApiKeyEntry(Guid TenantId, Guid Id, Guid ConsumerId, string KeyPrefix, DateTimeOffset? ExpiresAt);

/// <summary>
/// Toàn bộ cấu hình Gateway dựng từ một <see cref="ConfigSnapshotDto"/>. Bất biến: snapshot mới
/// thì dựng state mới và thay nguyên khối. Engine key được giải mã MỘT lần ở đây và chỉ nằm
/// trong bộ nhớ.
/// </summary>
internal sealed class GatewayState
{
    private readonly IReadOnlyDictionary<string, ApiKeyEntry> _apiKeysByHash;
    private readonly IReadOnlyDictionary<Guid, QuotaLimits> _quotas;
    private readonly IReadOnlyDictionary<Guid, MonthlyUsage> _monthlyUsage;
    private readonly IReadOnlyDictionary<Guid, string?> _engineKeys;

    private GatewayState(
        string etag,
        RoutingTable routing,
        IReadOnlyList<DeploymentEntry> deployments,
        IReadOnlyList<VirtualModelEntry> virtualModels,
        IReadOnlyList<ModelEntry> models,
        IReadOnlyDictionary<string, ApiKeyEntry> apiKeysByHash,
        IReadOnlyDictionary<Guid, QuotaLimits> quotas,
        IReadOnlyDictionary<Guid, MonthlyUsage> monthlyUsage,
        IReadOnlyDictionary<Guid, string?> engineKeys)
    {
        ETag = etag;
        Routing = routing;
        Deployments = deployments;
        VirtualModels = virtualModels;
        Models = models;
        _apiKeysByHash = apiKeysByHash;
        _quotas = quotas;
        _monthlyUsage = monthlyUsage;
        _engineKeys = engineKeys;
    }

    public string ETag { get; }

    public RoutingTable Routing { get; }

    public IReadOnlyList<DeploymentEntry> Deployments { get; }

    public IReadOnlyList<VirtualModelEntry> VirtualModels { get; }

    public IReadOnlyList<ModelEntry> Models { get; }

    public ApiKeyEntry? FindApiKey(string keyHash) => _apiKeysByHash.GetValueOrDefault(keyHash);

    public QuotaLimits QuotaFor(Guid consumerId) => _quotas.GetValueOrDefault(consumerId, QuotaLimits.Unlimited);

    public MonthlyUsage MonthlyUsageFor(Guid consumerId) => _monthlyUsage.GetValueOrDefault(consumerId, MonthlyUsage.None);

    public string? EngineKeyFor(Guid deploymentId) => _engineKeys.GetValueOrDefault(deploymentId);

    /// <summary>
    /// Dựng state từ snapshot. Entry có giá trị enum lạ (lệch phiên bản CP/GW) bị bỏ qua và báo
    /// qua <paramref name="warnings"/> thay vì làm hỏng cả snapshot.
    /// </summary>
    public static GatewayState From(ConfigSnapshotDto snapshot, string etag, ISecretProtector secrets, ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(warnings);

        var models = new List<ModelEntry>();
        foreach (var m in snapshot.Models)
        {
            var capabilities = new HashSet<ModelCapability>();
            foreach (var c in m.Capabilities)
            {
                if (EnumText.TryParse<ModelCapability>(c, out var capability))
                {
                    capabilities.Add(capability);
                }
            }

            models.Add(new ModelEntry(m.TenantId, m.Id, m.Name, capabilities));
        }

        var deployments = new List<DeploymentEntry>();
        var engineKeys = new Dictionary<Guid, string?>();
        foreach (var d in snapshot.Deployments)
        {
            if (!EnumText.TryParse<HealthStatus>(d.HealthStatus, out var health) || !Uri.TryCreate(d.BaseUrl, UriKind.Absolute, out var baseUrl))
            {
                warnings.Add($"Bỏ deployment {d.Id}: health/base_url không hợp lệ.");
                continue;
            }

            var kind = EnumText.TryParse<ProviderKind>(d.ProviderKind, out var parsedKind) ? parsedKind : ProviderKind.OpenAiCompatible;
            deployments.Add(new DeploymentEntry(d.TenantId, d.Id, d.ModelId, kind, baseUrl, d.RemoteModelName, health, d.Enabled, d.LatencyMsP50));

            string? engineKey = null;
            if (d.ApiKeyEncrypted is { } cipher)
            {
                engineKey = secrets.Unprotect(cipher);
                if (engineKey is null)
                {
                    warnings.Add($"Không giải mã được API key engine của deployment {d.Id} (key ring khác CP?).");
                }
            }

            engineKeys[d.Id] = engineKey;
        }

        var virtualModels = new List<VirtualModelEntry>();
        foreach (var v in snapshot.VirtualModels)
        {
            if (EnumText.TryParse<TaskKind>(v.Task, out var task))
            {
                virtualModels.Add(new VirtualModelEntry(v.TenantId, v.Id, v.Name, task));
            }
            else
            {
                warnings.Add($"Bỏ virtual model {v.Name}: task '{v.Task}' không hợp lệ.");
            }
        }

        var routes = snapshot.Routes.Select(r => new RouteEntry(r.TenantId, r.VirtualModelId, r.DeploymentId, r.Priority, r.Weight, r.Enabled)).ToList();

        return new GatewayState(
            etag,
            new RoutingTable(models, deployments, virtualModels, routes),
            deployments,
            virtualModels,
            models,
            snapshot.ApiKeys.ToDictionary(k => k.KeyHash, k => new ApiKeyEntry(k.TenantId, k.Id, k.ConsumerId, k.KeyPrefix, k.ExpiresAt), StringComparer.Ordinal),
            snapshot.Quotas.ToDictionary(q => q.ConsumerId, q => new QuotaLimits(q.TokensPerMinute, q.TokensPerMonth, q.MaxConcurrentRequests)),
            snapshot.MonthlyUsage.ToDictionary(u => u.ConsumerId, u => new MonthlyUsage(u.Tokens, u.AsOf)),
            engineKeys);
    }
}
