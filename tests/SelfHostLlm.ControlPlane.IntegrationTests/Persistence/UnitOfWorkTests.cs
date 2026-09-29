using Microsoft.EntityFrameworkCore;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Persistence.Repositories;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

[Collection(PostgresDatabase.Name)]
public sealed class UnitOfWorkTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SaveChangesAsync_DuplicateModelName_ReturnsConflict()
    {
        var tenant = TestData.Tenant();
        var existing = TestData.ChatModel(tenant.Id);
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenant, existing);
        }

        await using var db = fixture.CreateDbContext();
        var duplicate = Model.Create(tenant.Id, existing.Name, "qwen2.5", "7B", "Q4_K_M", 4096,
            [ModelCapability.Chat], [], TestData.Now).Value;
        new TenantRepository<Model>(db).Add(duplicate);

        var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Message.Should().Contain("ix_model_tenant_id_name");
    }

    [Fact]
    public async Task SaveChangesAsync_DeploymentReferencingOtherTenantModel_ReturnsValidationInsteadOfThrowing()
    {
        var tenantA = TestData.Tenant();
        var tenantB = TestData.Tenant();
        var modelOfA = TestData.ChatModel(tenantA.Id);
        var providerOfB = TestData.Provider(tenantB.Id);
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenantA, tenantB, modelOfA, providerOfB);
        }

        await using var db = fixture.CreateDbContext();
        db.Deployments.Add(TestData.Deployment(tenantB.Id, modelOfA.Id, providerOfB.Id));

        var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be("persistence.invalid_reference");
        db.ChangeTracker.Entries().Should().BeEmpty("thay đổi hỏng phải được bỏ");
    }

    [Fact]
    public async Task SaveChangesAsync_DeletingModelStillUsedByDeployment_ReturnsConflict()
    {
        var tenant = TestData.Tenant();
        var model = TestData.ChatModel(tenant.Id);
        var provider = TestData.Provider(tenant.Id);
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenant, model, provider, TestData.Deployment(tenant.Id, model.Id, provider.Id));
        }

        await using var db = fixture.CreateDbContext();
        var repository = new TenantRepository<Model>(db);
        repository.Remove((await repository.GetAsync(tenant.Id, model.Id, CancellationToken.None))!);

        var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        result.Error!.Code.Should().Be("persistence.in_use");
    }

    [Fact]
    public async Task SaveChangesAsync_ChangeAndAuditLog_ArePersistedTogether()
    {
        var tenant = TestData.Tenant();
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenant);
        }

        await using var db = fixture.CreateDbContext();
        var model = TestData.ChatModel(tenant.Id);
        new TenantRepository<Model>(db).Add(model);
        new AuditLogWriter(db).Add(AuditLog.Create(tenant.Id, null, AuditAction.Create, nameof(Model), model.Id.ToString(),
            null, """{"name":"x"}""", null, TestData.Now).Value);

        (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();

        await using var read = fixture.CreateDbContext();
        (await read.Models.AnyAsync(m => m.Id == model.Id)).Should().BeTrue();
        (await read.AuditLogs.AnyAsync(a => a.EntityId == model.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_FailedChange_DoesNotPersistItsAuditLog()
    {
        var tenant = TestData.Tenant();
        var existing = TestData.ChatModel(tenant.Id);
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenant, existing);
        }

        await using var db = fixture.CreateDbContext();
        var duplicate = Model.Create(tenant.Id, existing.Name, "qwen2.5", "7B", "Q4_K_M", 4096,
            [ModelCapability.Chat], [], TestData.Now).Value;
        db.Models.Add(duplicate);
        new AuditLogWriter(db).Add(AuditLog.Create(tenant.Id, null, AuditAction.Create, nameof(Model), duplicate.Id.ToString(),
            null, null, null, TestData.Now).Value);

        (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsFailure.Should().BeTrue();

        await using var read = fixture.CreateDbContext();
        (await read.AuditLogs.AnyAsync(a => a.EntityId == duplicate.Id.ToString()))
            .Should().BeFalse("audit và thay đổi nằm chung một transaction");
    }
}
