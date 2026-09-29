using Microsoft.Extensions.Time.Testing;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Deployments;

namespace SelfHostLlm.Application.UnitTests.Auditing;

public sealed class AuditTrailTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeAuditLogWriter _writer = new();
    private readonly FakeActor _actor = new();
    private readonly AuditTrail _trail;

    public AuditTrailTests()
    {
        _trail = new AuditTrail(_writer, _actor, new FakeTimeProvider(Now));
    }

    [Fact]
    public void Record_CapturesActorIpTimeAndCamelCaseJson()
    {
        var consumer = Consumer.Create(_actor.TenantId, "App nội bộ", null).Value;

        _trail.Record(_actor.TenantId, AuditAction.Create, nameof(Consumer), consumer.Id.ToString(), null, AuditSnapshot.Of(consumer));

        var log = _writer.Logs.Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(_actor.UserId);
        log.IpAddress.Should().Be(_actor.IpAddress);
        log.OccurredAt.Should().Be(Now);
        log.BeforeValue.Should().BeNull();
        log.AfterValue.Should().Contain("\"name\":").And.Contain("\"enabled\":true");
    }

    [Fact]
    public void ApiKeySnapshot_NeverContainsKeyHash()
    {
        var hash = new string('c', ApiKey.KeyHashLength);
        var key = ApiKey.Create(_actor.TenantId, Guid.NewGuid(), hash, "sk-abcdefg", null, Now).Value;

        _trail.Record(_actor.TenantId, AuditAction.Create, nameof(ApiKey), key.Id.ToString(), null, AuditSnapshot.Of(key));

        _writer.Logs[0].AfterValue.Should().NotContain(hash).And.Contain("sk-abcdefg");
    }

    [Fact]
    public void DeploymentSnapshot_RecordsOnlyWhetherEngineKeyExists()
    {
        var deployment = Deployment.Create(_actor.TenantId, Guid.NewGuid(), null, Guid.NewGuid(),
            Address.Create("http://192.168.1.50:8000").Value, "qwen", "CfDJ8-ciphertext-value").Value;

        _trail.Record(_actor.TenantId, AuditAction.Create, nameof(Deployment), deployment.Id.ToString(), null, AuditSnapshot.Of(deployment));

        _writer.Logs[0].AfterValue.Should().NotContain("CfDJ8").And.Contain("\"hasApiKey\":true");
    }

    [Fact]
    public void SnapshotTypes_HaveNoSecretBearingProperties()
    {
        string[] forbidden = ["KeyHash", "ApiKeyEncrypted", "PlainText", "Hash", "Secret", "Password"];

        var snapshotProperties = typeof(AuditSnapshot).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(AuditSnapshot).Namespace && t.Name.EndsWith("Snapshot", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        snapshotProperties.Should().NotBeEmpty();
        snapshotProperties.Should().NotContain(p => forbidden.Any(f => p.EndsWith("." + f, StringComparison.Ordinal)));
    }
}
