using Microsoft.EntityFrameworkCore;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

/// <summary>Trigger trong migration chặn mọi thao tác sửa/xoá audit_log, kể cả SQL đi vòng qua API.</summary>
[Collection(PostgresDatabase.Name)]
public sealed class AuditLogAppendOnlyTests(PostgresFixture fixture)
{
    private const string InsufficientPrivilege = "42501";

    private async Task<AuditLog> InsertAuditLogAsync()
    {
        await using var db = fixture.CreateDbContext();
        var tenant = TestData.Tenant();
        var log = AuditLog.Create(tenant.Id, null, AuditAction.Create, "Deployment", Guid.NewGuid().ToString(),
            null, """{"enabled":true}""", "192.168.1.10", TestData.Now).Value;
        await TestData.SaveAsync(db, tenant, log);
        return log;
    }

    [Fact]
    public async Task Update_OnAuditLog_IsRejectedByTrigger()
    {
        var log = await InsertAuditLogAsync();
        await using var db = fixture.CreateDbContext();

        var error = await PostgresAssertions.ThrowsPostgresAsync(() =>
            db.Database.ExecuteSqlAsync($"UPDATE audit_log SET entity_type = 'Tampered' WHERE id = {log.Id}"));

        error.SqlState.Should().Be(InsufficientPrivilege);
        (await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == log.Id)).EntityType.Should().Be("Deployment");
    }

    [Fact]
    public async Task Delete_OnAuditLog_IsRejectedByTrigger()
    {
        var log = await InsertAuditLogAsync();
        await using var db = fixture.CreateDbContext();

        var error = await PostgresAssertions.ThrowsPostgresAsync(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM audit_log WHERE id = {log.Id}"));

        error.SqlState.Should().Be(InsufficientPrivilege);
        (await db.AuditLogs.AnyAsync(a => a.Id == log.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Truncate_OnAuditLog_IsRejectedByTrigger()
    {
        await InsertAuditLogAsync();
        await using var db = fixture.CreateDbContext();

        var error = await PostgresAssertions.ThrowsPostgresAsync(() =>
            db.Database.ExecuteSqlAsync($"TRUNCATE audit_log"));

        error.SqlState.Should().Be(InsufficientPrivilege);
    }
}
