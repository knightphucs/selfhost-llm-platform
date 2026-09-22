using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;

namespace SelfHostLlm.Domain.UnitTests.Deployments;

public sealed class DeploymentTests
{
    private const int Threshold = 3;

    private static Deployment NewDeployment() => Deployment.Create(
        tenantId: Guid.NewGuid(),
        modelId: Guid.NewGuid(),
        modelVersionId: null,
        providerId: Guid.NewGuid(),
        address: Address.Create("http://192.168.1.50:11434").Value,
        remoteModelName: "qwen2.5:7b-instruct-q4_K_M",
        apiKeyEncrypted: null).Value;

    [Fact]
    public void Create_NewDeployment_IsEnabledWithUnknownHealth()
    {
        var deployment = NewDeployment();

        deployment.Enabled.Should().BeTrue();
        deployment.HealthStatus.Should().Be(HealthStatus.Unknown);
        deployment.IsRoutable.Should().BeTrue();
    }

    [Fact]
    public void Create_WithBlankRemoteModelName_ReturnsValidationError()
    {
        var result = Deployment.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            Address.Create("http://localhost:11434").Value, " ", null);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void RecordProbeFailure_BelowThreshold_KeepsHealthy()
    {
        var deployment = NewDeployment();
        deployment.RecordProbeSuccess(120, TestClock.Now);

        deployment.RecordProbeFailure(TestClock.Now, Threshold);
        deployment.RecordProbeFailure(TestClock.Now, Threshold);

        deployment.HealthStatus.Should().Be(HealthStatus.Healthy);
        deployment.ConsecutiveFailures.Should().Be(2);
        deployment.IsRoutable.Should().BeTrue();
    }

    [Fact]
    public void RecordProbeFailure_ReachingThreshold_BecomesUnhealthyAndNotRoutable()
    {
        var deployment = NewDeployment();

        for (var i = 0; i < Threshold; i++)
        {
            deployment.RecordProbeFailure(TestClock.Now, Threshold);
        }

        deployment.HealthStatus.Should().Be(HealthStatus.Unhealthy);
        deployment.IsRoutable.Should().BeFalse();
    }

    [Fact]
    public void RecordProbeSuccess_AfterFailures_ResetsCounterAndBecomesHealthy()
    {
        var deployment = NewDeployment();
        for (var i = 0; i < Threshold; i++)
        {
            deployment.RecordProbeFailure(TestClock.Now, Threshold);
        }

        deployment.RecordProbeSuccess(85, TestClock.Now.AddSeconds(30));

        deployment.HealthStatus.Should().Be(HealthStatus.Healthy);
        deployment.ConsecutiveFailures.Should().Be(0);
        deployment.LatencyMsP50.Should().Be(85);
        deployment.LastProbedAt.Should().Be(TestClock.Now.AddSeconds(30));
    }

    [Fact]
    public void Disable_HealthyDeployment_IsNotRoutable()
    {
        var deployment = NewDeployment();
        deployment.RecordProbeSuccess(50, TestClock.Now);

        deployment.Disable();

        deployment.IsRoutable.Should().BeFalse();
    }

    [Fact]
    public void RecordProbeSuccess_WithNonUtcTime_StoresUtc()
    {
        var deployment = NewDeployment();
        var local = new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.FromHours(7));

        deployment.RecordProbeSuccess(10, local);

        deployment.LastProbedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
        deployment.LastProbedAt.Should().Be(local);
    }
}
