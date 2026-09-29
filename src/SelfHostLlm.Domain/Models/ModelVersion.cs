using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Models;

/// <summary>
/// Phiên bản fine-tune của một <see cref="Model"/>. Trọng số adapter không lưu trong DB,
/// chỉ lưu URI (<see cref="AdapterUri"/>).
/// </summary>
public sealed class ModelVersion : Entity<Guid>, ITenantScoped
{
    private Dictionary<string, double> _evalMetrics = [];

    private ModelVersion()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ModelId { get; private set; }

    public string VersionTag { get; private set; } = null!;

    public string AdapterUri { get; private set; } = null!;

    /// <summary>Job sinh ra version này; null nếu adapter được đăng ký tay.</summary>
    public Guid? TrainingJobId { get; private set; }

    public IReadOnlyDictionary<string, double> EvalMetrics => _evalMetrics;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<ModelVersion> Create(
        Guid tenantId,
        Guid modelId,
        string versionTag,
        string adapterUri,
        Guid? trainingJobId,
        IReadOnlyDictionary<string, double>? evalMetrics,
        DateTimeOffset now)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "model_version.tenant_id"),
            Guard.NotEmpty(modelId, "model_version.model_id"),
            Guard.NotBlank(versionTag, "model_version.version_tag", 100),
            Guard.NotBlank(adapterUri, "model_version.adapter_uri", 1000));
        if (error is not null)
        {
            return error;
        }

        return new ModelVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModelId = modelId,
            VersionTag = versionTag.Trim(),
            AdapterUri = adapterUri.Trim(),
            TrainingJobId = trainingJobId,
            _evalMetrics = evalMetrics is null ? [] : new Dictionary<string, double>(evalMetrics),
            CreatedAt = now.ToUniversalTime(),
        };
    }
}
