using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Persistence.Configuration;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

[Collection(PostgresDatabase.Name)]
public sealed class ConfigSnapshotSourceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task LoadAsync_IncludesOnlyUsableApiKeysAndThisMonthUsage()
    {
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var tenant = TestData.Tenant();
        var active = TestData.Consumer(tenant.Id);
        var disabled = TestData.Consumer(tenant.Id);
        disabled.Disable();
        var usable = ApiKey.Create(tenant.Id, active.Id, TestData.RandomHash(), "sk-usable", null, TestData.Now).Value;
        var revoked = ApiKey.Create(tenant.Id, active.Id, TestData.RandomHash(), "sk-revoke", null, TestData.Now).Value;
        revoked.Revoke(TestData.Now);
        var expired = ApiKey.Create(tenant.Id, active.Id, TestData.RandomHash(), "sk-expire", TestData.Now.AddDays(1), TestData.Now).Value;
        var ofDisabledConsumer = ApiKey.Create(tenant.Id, disabled.Id, TestData.RandomHash(), "sk-disabl", null, TestData.Now).Value;

        UsageRecord Usage(int tokens, DateTimeOffset at) => UsageRecord.Create(tenant.Id, usable.Id, null, "chat-general", null,
            TokenCount.Create(tokens, 0, false).Value, 10, 200, false, at).Value;

        await using (var seed = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(seed, tenant, active, disabled, usable, revoked, expired, ofDisabledConsumer,
                Usage(100, new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero)),
                Usage(50, new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero)),
                Usage(999, new DateTimeOffset(2026, 8, 31, 23, 0, 0, TimeSpan.Zero)));
        }

        await using var db = fixture.CreateDbContext();
        var snapshot = await new ConfigSnapshotSource(db).LoadAsync(now, CancellationToken.None);

        var keysOfTenant = snapshot.ApiKeys.Where(k => k.TenantId == tenant.Id).Select(k => k.KeyPrefix);
        keysOfTenant.Should().Equal("sk-usable");
        snapshot.MonthlyUsage.Should().ContainSingle(u => u.ConsumerId == active.Id).Which.Tokens.Should().Be(150);
        snapshot.GeneratedAt.Should().Be(now);
    }
}
