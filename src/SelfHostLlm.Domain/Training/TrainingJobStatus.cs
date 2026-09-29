namespace SelfHostLlm.Domain.Training;

public enum TrainingJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}
