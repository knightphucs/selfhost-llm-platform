using SelfHostLlm.Application.Access;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Access;

public sealed class AccessUseCaseTests
{
    private readonly UseCaseHarness _h = new();

    private async Task<Consumer> NewConsumerAsync() =>
        (await _h.SendAsync(new CreateConsumerCommand(_h.TenantId, "App nội bộ", null))).Value;

    [Fact]
    public async Task CreateApiKey_ReturnsPlaintextOnceAndStoresOnlyHash()
    {
        var consumer = await NewConsumerAsync();

        var created = (await _h.SendAsync(new CreateApiKeyCommand(_h.TenantId, consumer.Id, null))).Value;

        ApiKeyHasher.IsWellFormed(created.PlainText).Should().BeTrue();
        var stored = _h.Repository<ApiKey>().Items.Should().ContainSingle().Subject;
        stored.KeyHash.Should().Be(new ApiKeyHasher().ComputeHash(created.PlainText));
        stored.KeyPrefix.Should().Be(created.PlainText[..ApiKeyHasher.DisplayPrefixLength]);
        created.ToString().Should().NotContain(created.PlainText);
    }

    [Fact]
    public async Task CreateApiKey_AuditContainsNeitherPlaintextNorHash()
    {
        var consumer = await NewConsumerAsync();

        var created = (await _h.SendAsync(new CreateApiKeyCommand(_h.TenantId, consumer.Id, null))).Value;

        var audit = _h.AuditLogs[^1];
        audit.EntityType.Should().Be(nameof(ApiKey));
        audit.AfterValue.Should().NotContain(created.PlainText).And.NotContain(created.ApiKey.KeyHash)
            .And.Contain(created.ApiKey.KeyPrefix);
    }

    [Fact]
    public async Task CreateApiKey_ForConsumerOfOtherTenant_ReturnsNotFound()
    {
        var foreignConsumer = Consumer.Create(Guid.NewGuid(), "x", null).Value;
        _h.Repository<Consumer>().Items.Add(foreignConsumer);

        var result = await _h.SendAsync(new CreateApiKeyCommand(_h.TenantId, foreignConsumer.Id, null));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        _h.Repository<ApiKey>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateApiKey_WhenSaveFails_DoesNotReturnPlaintext()
    {
        var consumer = await NewConsumerAsync();
        _h.UnitOfWork.NextError = Error.Conflict("persistence.duplicate", "trùng");

        var result = await _h.SendAsync(new CreateApiKeyCommand(_h.TenantId, consumer.Id, null));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeApiKey_Twice_SecondReturnsConflict()
    {
        var consumer = await NewConsumerAsync();
        var created = (await _h.SendAsync(new CreateApiKeyCommand(_h.TenantId, consumer.Id, null))).Value;

        (await _h.SendAsync(new RevokeApiKeyCommand(_h.TenantId, created.ApiKey.Id))).Value.IsRevoked.Should().BeTrue();
        _h.AuditLogs[^1].Action.Should().Be(AuditAction.Revoke);

        (await _h.SendAsync(new RevokeApiKeyCommand(_h.TenantId, created.ApiKey.Id))).Error!.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public async Task CreateApiKey_WithPastExpiry_ReturnsValidation()
    {
        var consumer = await NewConsumerAsync();

        var result = await _h.SendAsync(new CreateApiKeyCommand(_h.TenantId, consumer.Id, UseCaseHarness.Now.AddDays(-1)));

        result.Error!.Code.Should().Be("api_key.expires_at.past");
    }

    [Fact]
    public async Task UpsertQuota_CreatesThenUpdates_WithMatchingAudit()
    {
        var consumer = await NewConsumerAsync();

        var created = (await _h.SendAsync(new UpsertQuotaCommand(_h.TenantId, consumer.Id, 1000, null, 2))).Value;
        var updated = (await _h.SendAsync(new UpsertQuotaCommand(_h.TenantId, consumer.Id, 5000, 1_000_000, null))).Value;

        updated.Id.Should().Be(created.Id);
        updated.TokensPerMinute.Should().Be(5000);
        updated.MaxConcurrentRequests.Should().BeNull();
        _h.Repository<Quota>().Items.Should().ContainSingle();
        _h.AuditLogs.Where(a => a.EntityType == nameof(Quota)).Select(a => a.Action)
            .Should().Equal(AuditAction.Create, AuditAction.Update);
    }

    [Fact]
    public async Task UpsertQuota_NonPositiveLimit_ReturnsValidation()
    {
        var consumer = await NewConsumerAsync();

        (await _h.SendAsync(new UpsertQuotaCommand(_h.TenantId, consumer.Id, 0, null, null))).Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task UpdateConsumer_Disable_IsAudited()
    {
        var consumer = await NewConsumerAsync();

        var result = await _h.SendAsync(new UpdateConsumerCommand(_h.TenantId, consumer.Id, "App nội bộ", null, Enabled: false));

        result.Value.Enabled.Should().BeFalse();
        _h.AuditLogs[^1].BeforeValue.Should().Contain("\"enabled\":true");
        _h.AuditLogs[^1].AfterValue.Should().Contain("\"enabled\":false");
    }
}
