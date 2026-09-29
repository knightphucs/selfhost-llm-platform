using SelfHostLlm.Application.Security;
using SelfHostLlm.Contracts.Internal;

namespace SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

/// <summary>Dựng config snapshot cho test: một tenant, một model chat, các deployment theo thứ tự priority.</summary>
public sealed class SnapshotBuilder
{
    private readonly List<SnapshotModelDto> _models = [];
    private readonly List<SnapshotDeploymentDto> _deployments = [];
    private readonly List<SnapshotVirtualModelDto> _virtualModels = [];
    private readonly List<SnapshotRouteDto> _routes = [];
    private readonly List<SnapshotApiKeyDto> _apiKeys = [];
    private readonly List<SnapshotQuotaDto> _quotas = [];

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid ConsumerId { get; } = Guid.NewGuid();

    public string ApiKey { get; private set; } = "";

    public Guid ApiKeyId { get; } = Guid.NewGuid();

    public SnapshotBuilder WithApiKey(Guid? tenantId = null, Guid? consumerId = null)
    {
        var generated = new ApiKeyHasher().Generate();
        ApiKey = generated.PlainText;
        _apiKeys.Add(new SnapshotApiKeyDto(tenantId ?? TenantId, ApiKeyId, consumerId ?? ConsumerId, generated.Hash, generated.Prefix, null));
        return this;
    }

    public Guid Model(string name, string capability = "Chat", Guid? tenantId = null)
    {
        var id = Guid.NewGuid();
        _models.Add(new SnapshotModelDto(tenantId ?? TenantId, id, name, [capability]));
        return id;
    }

    public Guid Deployment(Guid modelId, Uri baseUrl, string remoteModel = "qwen2.5:7b-instruct", string? engineKeyCiphertext = null,
        string health = "Healthy", Guid? tenantId = null)
    {
        var id = Guid.NewGuid();
        _deployments.Add(new SnapshotDeploymentDto(tenantId ?? TenantId, id, modelId, "Ollama", baseUrl.ToString(), remoteModel, health, true, 50, engineKeyCiphertext));
        return id;
    }

    /// <summary>Virtual model với chuỗi deployment theo đúng thứ tự truyền vào (priority 0, 1, 2...).</summary>
    public SnapshotBuilder VirtualModel(string name, string task, params Guid[] deploymentsInPriorityOrder) =>
        VirtualModelFor(TenantId, name, task, deploymentsInPriorityOrder);

    public SnapshotBuilder VirtualModelFor(Guid tenantId, string name, string task, params Guid[] deploymentsInPriorityOrder)
    {
        var id = Guid.NewGuid();
        _virtualModels.Add(new SnapshotVirtualModelDto(tenantId, id, name, task));
        for (var i = 0; i < deploymentsInPriorityOrder.Length; i++)
        {
            _routes.Add(new SnapshotRouteDto(tenantId, id, deploymentsInPriorityOrder[i], i, 1, true));
        }

        return this;
    }

    public SnapshotBuilder Quota(int? tokensPerMinute = null, long? tokensPerMonth = null, int? maxConcurrent = null)
    {
        _quotas.Add(new SnapshotQuotaDto(TenantId, ConsumerId, tokensPerMinute, tokensPerMonth, maxConcurrent));
        return this;
    }

    public ConfigSnapshotDto Build() =>
        new(DateTimeOffset.UtcNow, _models.ToList(), _deployments.ToList(), _virtualModels.ToList(), _routes.ToList(), _apiKeys.ToList(), _quotas.ToList(), []);
}
