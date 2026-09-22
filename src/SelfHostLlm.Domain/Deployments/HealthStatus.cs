namespace SelfHostLlm.Domain.Deployments;

public enum HealthStatus
{
    /// <summary>Chưa probe lần nào.</summary>
    Unknown,
    Healthy,
    Unhealthy,
}
