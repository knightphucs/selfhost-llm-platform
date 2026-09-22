using Microsoft.EntityFrameworkCore;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Rag;
using SelfHostLlm.Persistence.VectorStore;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Rag;

/// <summary>
/// Test bắt buộc của báo cáo: tenant B không bao giờ truy vấn được chunk của tenant A.
/// Hai tenant cố ý có chunk mang vector giống hệt nhau — chỉ filter tenant_id trong WHERE mới
/// phân biệt được.
/// </summary>
[Collection(PostgresDatabase.Name)]
public sealed class VectorStoreTenantIsolationTests(PostgresFixture fixture)
{
    private sealed record TenantCorpus(Guid TenantId, Guid CollectionId, Guid DocumentId, IReadOnlyList<Guid> ChunkIds);

    private async Task<TenantCorpus> SeedTenantAsync(params int[] unitVectorIndexes)
    {
        await using var db = fixture.CreateDbContext();
        var tenant = TestData.Tenant();
        var embeddingModel = TestData.EmbeddingModel(tenant.Id);
        var collection = TestData.Collection(tenant.Id, embeddingModel.Id);
        var document = TestData.Document(tenant.Id, collection.Id);
        await TestData.SaveAsync(db, tenant, embeddingModel, collection, document);

        var chunks = unitVectorIndexes
            .Select((axis, ordinal) => TestData.Chunk(tenant.Id, collection.Id, document.Id, ordinal, TestData.UnitVector(axis)))
            .ToList();
        (await new PgVectorStore(db).AddChunksAsync(tenant.Id, chunks, CancellationToken.None)).IsSuccess.Should().BeTrue();

        return new TenantCorpus(tenant.Id, collection.Id, document.Id, chunks.Select(c => c.Id).ToList());
    }

    [Fact]
    public async Task SearchAsync_TenantB_NeverSeesTenantAChunks()
    {
        var tenantA = await SeedTenantAsync(0, 1);
        var tenantB = await SeedTenantAsync(0, 2);
        await using var db = fixture.CreateDbContext();
        var store = new PgVectorStore(db);

        // Tenant B truy vấn bằng đúng vector trùng với chunk của A, lấy k lớn.
        var result = await store.SearchAsync(tenantB.TenantId, tenantB.CollectionId, TestData.UnitVector(0), 100, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(m => m.ChunkId).Should().BeEquivalentTo(tenantB.ChunkIds);
        result.Value.Select(m => m.ChunkId).Should().NotIntersectWith(tenantA.ChunkIds);
    }

    [Fact]
    public async Task SearchAsync_TenantBUsingTenantACollectionId_ReturnsNothing()
    {
        var tenantA = await SeedTenantAsync(0, 1);
        var tenantB = await SeedTenantAsync(3);
        await using var db = fixture.CreateDbContext();

        // Kịch bản tấn công: B đoán/lấy được collectionId của A.
        var result = await new PgVectorStore(db)
            .SearchAsync(tenantB.TenantId, tenantA.CollectionId, TestData.UnitVector(0), 100, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ReturnsNearestFirst()
    {
        var corpus = await SeedTenantAsync(4, 5, 6);
        await using var db = fixture.CreateDbContext();
        var query = TestData.UnitVector(5);
        query[4] = 0.1f;

        var result = await new PgVectorStore(db).SearchAsync(corpus.TenantId, corpus.CollectionId, query, 2, CancellationToken.None);

        result.Value.Should().HaveCount(2);
        result.Value[0].ChunkId.Should().Be(corpus.ChunkIds[1]);
        result.Value[1].ChunkId.Should().Be(corpus.ChunkIds[0]);
        result.Value[0].Distance.Should().BeLessThan(result.Value[1].Distance);
    }

    [Fact]
    public async Task AddChunksAsync_WithChunkOfOtherTenant_ReturnsForbiddenAndWritesNothing()
    {
        var tenantA = await SeedTenantAsync(0);
        var tenantB = await SeedTenantAsync(0);
        await using var db = fixture.CreateDbContext();
        var foreign = TestData.Chunk(tenantA.TenantId, tenantA.CollectionId, tenantA.DocumentId, 99, TestData.UnitVector(9));

        var result = await new PgVectorStore(db).AddChunksAsync(tenantB.TenantId, [foreign], CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await db.Chunks.AnyAsync(c => c.Id == foreign.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData(768, 5)]
    [InlineData(EmbeddingConstants.Dimension, 0)]
    [InlineData(EmbeddingConstants.Dimension, 101)]
    public async Task SearchAsync_WithInvalidInput_ReturnsValidation(int dimension, int topK)
    {
        await using var db = fixture.CreateDbContext();

        var result = await new PgVectorStore(db)
            .SearchAsync(Guid.NewGuid(), Guid.NewGuid(), new float[dimension], topK, CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }
}
