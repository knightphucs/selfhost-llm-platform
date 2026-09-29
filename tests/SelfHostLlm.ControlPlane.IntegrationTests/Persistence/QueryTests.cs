using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Persistence.Repositories;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

[Collection(PostgresDatabase.Name)]
public sealed class QueryTests(PostgresFixture fixture)
{
    private static UsageRecord Usage(Guid tenantId, Guid apiKeyId, int prompt, int completion, bool estimated, bool fallback, DateTimeOffset at) =>
        UsageRecord.Create(tenantId, apiKeyId, null, "chat-general", null,
            TokenCount.Create(prompt, completion, estimated).Value, 100, 200, fallback, at).Value;

    [Fact]
    public async Task SummaryAsync_AggregatesTokensAndCountsEstimatedAndFallback()
    {
        var tenant = TestData.Tenant();
        var otherTenant = TestData.Tenant();
        var consumer = TestData.Consumer(tenant.Id);
        var otherConsumer = TestData.Consumer(tenant.Id);
        var key = TestData.ApiKey(tenant.Id, consumer.Id);
        var otherKey = TestData.ApiKey(tenant.Id, otherConsumer.Id);
        var foreignConsumer = TestData.Consumer(otherTenant.Id);
        var foreignKey = TestData.ApiKey(otherTenant.Id, foreignConsumer.Id);
        var at = TestData.Now;
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenant, otherTenant, consumer, otherConsumer, key, otherKey, foreignConsumer, foreignKey,
                Usage(tenant.Id, key.Id, 100, 50, estimated: false, fallback: false, at),
                Usage(tenant.Id, key.Id, 10, 5, estimated: true, fallback: true, at.AddMinutes(1)),
                Usage(tenant.Id, otherKey.Id, 1000, 1000, estimated: false, fallback: false, at),
                Usage(tenant.Id, key.Id, 7, 7, estimated: false, fallback: false, at.AddDays(40)),
                Usage(otherTenant.Id, foreignKey.Id, 9999, 9999, estimated: true, fallback: true, at));
        }

        await using var db = fixture.CreateDbContext();
        var queries = new UsageQueries(db);

        var forConsumer = await queries.SummaryAsync(tenant.Id, consumer.Id, at, at.AddDays(30), CancellationToken.None);
        var forTenant = await queries.SummaryAsync(tenant.Id, null, at, at.AddDays(30), CancellationToken.None);

        forConsumer.Should().BeEquivalentTo(new { PromptTokens = 110L, CompletionTokens = 55L, RequestCount = 2L, EstimatedRequestCount = 1L, FallbackRequestCount = 1L });
        forTenant.TotalTokens.Should().Be(110 + 55 + 2000);
        forTenant.RequestCount.Should().Be(3);
    }

    [Fact]
    public async Task SummaryAsync_NoUsage_ReturnsZeros()
    {
        await using var db = fixture.CreateDbContext();

        var summary = await new UsageQueries(db).SummaryAsync(Guid.NewGuid(), null, TestData.Now, TestData.Now.AddDays(1), CancellationToken.None);

        summary.RequestCount.Should().Be(0);
        summary.TotalTokens.Should().Be(0);
    }

    [Fact]
    public async Task AuditLogListAsync_PagesNewestFirstWithinTenant()
    {
        var tenant = TestData.Tenant();
        var otherTenant = TestData.Tenant();
        var logs = Enumerable.Range(0, 5)
            .Select(i => AuditLog.Create(tenant.Id, null, AuditAction.Update, "Model", $"m{i}", null, null, null, TestData.Now.AddMinutes(i)).Value)
            .ToList();
        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, [tenant, otherTenant, .. logs,
                AuditLog.Create(otherTenant.Id, null, AuditAction.Update, "Model", "foreign", null, null, null, TestData.Now.AddHours(1)).Value]);
        }

        await using var db = fixture.CreateDbContext();
        var page = await new AuditLogQueries(db).ListAsync(
            tenant.Id, new AuditLogFilter(null, null, "Model", null, null), new PageRequest(2, 2), CancellationToken.None);

        page.Total.Should().Be(5);
        page.Items.Select(a => a.EntityId).Should().Equal("m2", "m1");
    }
}
