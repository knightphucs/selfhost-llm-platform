using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Domain.UnitTests.Audit;

public sealed class AuditLogTests
{
    [Fact]
    public void Create_WithBeforeAndAfter_StoresUtcSnapshot()
    {
        var local = new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.FromHours(7));

        var log = AuditLog.Create(
            Guid.NewGuid(), Guid.NewGuid(), AuditAction.Update, "Deployment", Guid.NewGuid().ToString(),
            """{"enabled":true}""", """{"enabled":false}""", "192.168.1.10", local).Value;

        log.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
        log.BeforeValue.Should().Contain("true");
        log.AfterValue.Should().Contain("false");
    }

    [Fact]
    public void Create_WithoutEntityType_ReturnsValidationError()
    {
        AuditLog.Create(Guid.NewGuid(), null, AuditAction.Create, "", "x", null, null, null, TestClock.Now)
            .Error!.Code.Should().Be("audit.entity_type.empty");
    }

    [Fact]
    public void AuditLog_HasNoPublicMutators()
    {
        typeof(AuditLog).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Should().BeEmpty("audit log là append-only");
        typeof(AuditLog).GetMethods()
            .Where(m => m.DeclaringType == typeof(AuditLog) && !m.IsStatic && !m.IsSpecialName)
            .Should().BeEmpty("audit log không có behavior sửa");
    }
}
