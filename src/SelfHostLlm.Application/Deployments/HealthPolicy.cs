using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Deployments;

namespace SelfHostLlm.Application.Deployments;

internal static class HealthPolicy
{
    /// <summary>Số lần probe lỗi liên tiếp trước khi coi deployment là Unhealthy — tránh nhấp nháy khi LAN chập chờn.</summary>
    public const int UnhealthyThreshold = 3;

    public static void Apply(Deployment deployment, ProbeResult probe, DateTimeOffset now)
    {
        if (probe.Healthy)
        {
            deployment.RecordProbeSuccess(probe.LatencyMs, now);
        }
        else
        {
            deployment.RecordProbeFailure(now, UnhealthyThreshold);
        }
    }
}
