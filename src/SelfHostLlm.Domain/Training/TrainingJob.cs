using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Training;

/// <summary>
/// Một lần fine-tune LoRA/QLoRA. Vòng đời:
/// <c>Queued → Running → Succeeded | Failed</c>, và <c>Queued | Running → Cancelled</c>.
/// Chuyển trạng thái sai trả <see cref="ErrorKind.Conflict"/>.
/// </summary>
public sealed class TrainingJob : Entity<Guid>, ITenantScoped
{
    private Dictionary<string, string> _hyperparameters = [];

    private TrainingJob()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BaseModelId { get; private set; }

    public Guid DatasetId { get; private set; }

    public TrainingMethod Method { get; private set; }

    /// <summary>Tham số truyền nguyên cho script Python (<c>r</c>, <c>lora_alpha</c>, <c>epochs</c>...).</summary>
    public IReadOnlyDictionary<string, string> Hyperparameters => _hyperparameters;

    public TrainingJobStatus Status { get; private set; }

    public string? LogUri { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public bool IsTerminal => Status is TrainingJobStatus.Succeeded or TrainingJobStatus.Failed or TrainingJobStatus.Cancelled;

    public static Result<TrainingJob> Create(
        Guid tenantId,
        Guid baseModelId,
        Guid datasetId,
        TrainingMethod method,
        IReadOnlyDictionary<string, string>? hyperparameters)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "training_job.tenant_id"),
            Guard.NotEmpty(baseModelId, "training_job.base_model_id"),
            Guard.NotEmpty(datasetId, "training_job.dataset_id"),
            Enum.IsDefined(method) ? null : Error.Validation("training_job.method.invalid", "Chỉ hỗ trợ LoRA hoặc QLoRA."));
        if (error is not null)
        {
            return error;
        }

        return new TrainingJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseModelId = baseModelId,
            DatasetId = datasetId,
            Method = method,
            _hyperparameters = hyperparameters is null ? [] : new Dictionary<string, string>(hyperparameters),
            Status = TrainingJobStatus.Queued,
        };
    }

    public Result Start(DateTimeOffset now, string? logUri)
    {
        if (Status != TrainingJobStatus.Queued)
        {
            return InvalidTransition(TrainingJobStatus.Running);
        }

        Status = TrainingJobStatus.Running;
        StartedAt = now.ToUniversalTime();
        LogUri = logUri;
        return Result.Success();
    }

    public Result Succeed(DateTimeOffset now) => Finish(TrainingJobStatus.Succeeded, now);

    public Result Fail(DateTimeOffset now) => Finish(TrainingJobStatus.Failed, now);

    public Result Cancel(DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return InvalidTransition(TrainingJobStatus.Cancelled);
        }

        Status = TrainingJobStatus.Cancelled;
        FinishedAt = now.ToUniversalTime();
        return Result.Success();
    }

    private Result Finish(TrainingJobStatus target, DateTimeOffset now)
    {
        if (Status != TrainingJobStatus.Running)
        {
            return InvalidTransition(target);
        }

        Status = target;
        FinishedAt = now.ToUniversalTime();
        return Result.Success();
    }

    private Error InvalidTransition(TrainingJobStatus target) => Error.Conflict(
        "training_job.invalid_transition",
        $"Không thể chuyển training job từ {Status} sang {target}.");
}
