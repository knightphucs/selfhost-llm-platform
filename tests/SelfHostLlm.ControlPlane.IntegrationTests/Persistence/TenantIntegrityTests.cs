using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

/// <summary>
/// Composite FK (tenant_id, x_id) → parent(tenant_id, id): DB từ chối tham chiếu chéo tenant
/// ngay cả khi tầng ứng dụng có bug và truyền nhầm id của tenant khác.
/// </summary>
[Collection(PostgresDatabase.Name)]
public sealed class TenantIntegrityTests(PostgresFixture fixture)
{
    private const string ForeignKeyViolation = "23503";

    [Fact]
    public async Task Deployment_ReferencingModelOfOtherTenant_IsRejectedByCompositeFk()
    {
        await using var db = fixture.CreateDbContext();
        var tenantA = TestData.Tenant();
        var tenantB = TestData.Tenant();
        var modelOfA = TestData.ChatModel(tenantA.Id);
        var providerOfB = TestData.Provider(tenantB.Id);
        await TestData.SaveAsync(db, tenantA, tenantB, modelOfA, providerOfB);

        var crossTenant = TestData.Deployment(tenantB.Id, modelOfA.Id, providerOfB.Id);
        var error = await PostgresAssertions.ThrowsPostgresAsync(() => TestData.SaveAsync(db, crossTenant));

        error.SqlState.Should().Be(ForeignKeyViolation);
        error.ConstraintName.Should().Be("fk_deployment_model_id");
    }

    [Fact]
    public async Task Route_ReferencingDeploymentOfOtherTenant_IsRejectedByCompositeFk()
    {
        await using var db = fixture.CreateDbContext();
        var tenantA = TestData.Tenant();
        var tenantB = TestData.Tenant();
        var model = TestData.ChatModel(tenantA.Id);
        var provider = TestData.Provider(tenantA.Id);
        var deploymentOfA = TestData.Deployment(tenantA.Id, model.Id, provider.Id);
        var virtualModelOfB = TestData.VirtualModel(tenantB.Id);
        await TestData.SaveAsync(db, tenantA, tenantB, model, provider, deploymentOfA, virtualModelOfB);

        var crossTenant = Route.Create(tenantB.Id, virtualModelOfB.Id, deploymentOfA.Id, priority: 0).Value;
        var error = await PostgresAssertions.ThrowsPostgresAsync(() => TestData.SaveAsync(db, crossTenant));

        error.SqlState.Should().Be(ForeignKeyViolation);
        error.ConstraintName.Should().Be("fk_route_deployment_id");
    }
}
