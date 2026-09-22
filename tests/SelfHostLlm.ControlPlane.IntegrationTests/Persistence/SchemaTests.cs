using Microsoft.EntityFrameworkCore;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

[Collection(PostgresDatabase.Name)]
public sealed class SchemaTests(PostgresFixture fixture)
{
    private const string CheckViolation = "23514";

    [Fact]
    public async Task Migrate_OnEmptyDatabase_CreatesVectorExtensionAndHnswIndex()
    {
        await using var db = fixture.CreateDbContext();

        var extensions = await db.Database
            .SqlQuery<string>($"SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'vector'")
            .ToListAsync();
        var hnswIndexes = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'ix_chunk_embedding_hnsw'")
            .ToListAsync();

        extensions.Should().ContainSingle();
        hnswIndexes.Should().ContainSingle().Which.Should().Contain("hnsw").And.Contain("vector_cosine_ops");
    }

    [Fact]
    public async Task SeededRoles_MatchDomainRolePermissions()
    {
        await using var db = fixture.CreateDbContext();

        var roles = await db.Roles.AsNoTracking().ToListAsync();

        roles.Select(r => r.Name).Should().BeEquivalentTo(SystemRoles.All);
        foreach (var role in roles)
        {
            role.Permissions.Should().BeEquivalentTo(RolePermissions.Default[role.Name!], because: $"role {role.Name}");
        }
    }

    [Fact]
    public async Task EnumColumn_WithUnknownValue_IsRejectedByCheckConstraint()
    {
        await using var db = fixture.CreateDbContext();
        var tenant = TestData.Tenant();
        await TestData.SaveAsync(db, tenant);

        var error = await PostgresAssertions.ThrowsPostgresAsync(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO provider (id, tenant_id, name, kind) VALUES ({Guid.NewGuid()}, {tenant.Id}, 'x', 'OpenAI')
            """));

        error.SqlState.Should().Be(CheckViolation);
        error.ConstraintName.Should().Be("ck_provider_kind");
    }

    [Fact]
    public async Task CollectionEmbeddingDim_OtherThan1024_IsRejectedByCheckConstraint()
    {
        await using var db = fixture.CreateDbContext();
        var tenant = TestData.Tenant();
        var model = TestData.EmbeddingModel(tenant.Id);
        await TestData.SaveAsync(db, tenant, model);

        var error = await PostgresAssertions.ThrowsPostgresAsync(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO collection (id, tenant_id, name, embedding_model_id, embedding_dim, chunk_size, chunk_overlap, created_at)
            VALUES ({Guid.NewGuid()}, {tenant.Id}, 'docs', {model.Id}, 768, 512, 64, now())
            """));

        error.ConstraintName.Should().Be("ck_collection_embedding_dim");
    }

    [Fact]
    public async Task ApiKey_WithDuplicateHash_IsRejectedByUniqueIndex()
    {
        await using var db = fixture.CreateDbContext();
        var tenant = TestData.Tenant();
        var consumer = TestData.Consumer(tenant.Id);
        var hash = TestData.RandomHash();
        await TestData.SaveAsync(db, tenant, consumer, TestData.ApiKey(tenant.Id, consumer.Id, hash));

        var error = await PostgresAssertions.ThrowsPostgresAsync(
            () => TestData.SaveAsync(db, TestData.ApiKey(tenant.Id, consumer.Id, hash)));

        error.ConstraintName.Should().Be("ix_api_key_key_hash");
    }
}
