using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Persistence.Repositories;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

/// <summary>Repository generic là điểm lọc tenant duy nhất — biết id cũng không đọc được dữ liệu tenant khác.</summary>
[Collection(PostgresDatabase.Name)]
public sealed class TenantRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetAsync_TenantBWithIdOfTenantAEntity_ReturnsNull()
    {
        var tenantA = TestData.Tenant();
        var tenantB = TestData.Tenant();
        var modelOfA = TestData.ChatModel(tenantA.Id);
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenantA, tenantB, modelOfA);
        }

        await using var db = fixture.CreateDbContext();
        var repository = new TenantRepository<Model>(db);

        (await repository.GetAsync(tenantB.Id, modelOfA.Id, CancellationToken.None)).Should().BeNull();
        (await repository.GetAsync(tenantA.Id, modelOfA.Id, CancellationToken.None))!.Id.Should().Be(modelOfA.Id);
        (await repository.ExistsAsync(tenantB.Id, m => m.Name == modelOfA.Name, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyEntitiesOfRequestedTenant_ThenAppliesFilter()
    {
        var tenantA = TestData.Tenant();
        var tenantB = TestData.Tenant();
        var chatA = TestData.ChatModel(tenantA.Id);
        var embedA = TestData.EmbeddingModel(tenantA.Id);
        var chatB = TestData.ChatModel(tenantB.Id);
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenantA, tenantB, chatA, embedA, chatB);
        }

        await using var db = fixture.CreateDbContext();
        var repository = new TenantRepository<Model>(db);

        (await repository.ListAsync(tenantA.Id, null, CancellationToken.None))
            .Select(m => m.Id).Should().BeEquivalentTo([chatA.Id, embedA.Id]);
        (await repository.ListAsync(tenantA.Id, m => m.Family == "bge", CancellationToken.None))
            .Select(m => m.Id).Should().Equal(embedA.Id);
    }

    [Fact]
    public async Task Methods_WithEmptyTenantId_Throw()
    {
        await using var db = fixture.CreateDbContext();
        var repository = new TenantRepository<Model>(db);

        var get = () => repository.GetAsync(Guid.Empty, Guid.NewGuid(), CancellationToken.None);

        await get.Should().ThrowAsync<ArgumentException>();
    }
}
