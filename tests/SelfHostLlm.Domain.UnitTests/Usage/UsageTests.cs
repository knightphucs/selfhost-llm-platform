using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Domain.Usage;

namespace SelfHostLlm.Domain.UnitTests.Usage;

public sealed class UsageTests
{
    [Fact]
    public void TokenCountCreate_WithNegativeTokens_ReturnsValidationError()
    {
        TokenCount.Create(-1, 10, false).Error!.Code.Should().Be("tokens.prompt.negative");
    }

    [Fact]
    public void TokenCountTotal_SumsPromptAndCompletion()
    {
        TokenCount.Create(120, 30, isEstimated: true).Value.Total.Should().Be(150);
    }

    [Fact]
    public void UsageRecordCreate_WithEstimatedTokens_KeepsEstimatedFlag()
    {
        var tokens = TokenCount.Create(100, 20, isEstimated: true).Value;

        var record = UsageRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "code-fast", TaskKind.Coding,
            tokens, latencyMs: 850, statusCode: 200, usedFallback: true, TestClock.Now).Value;

        record.TokensEstimated.Should().BeTrue();
        record.UsedFallback.Should().BeTrue();
        record.Tokens.Should().Be(tokens);
    }

    [Fact]
    public void UsageRecordCreate_WhenAllDeploymentsFailed_AllowsNullDeployment()
    {
        var record = UsageRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), deploymentId: null, "chat-general", null,
            TokenCount.Zero, latencyMs: 30_000, statusCode: 502, usedFallback: true, TestClock.Now);

        record.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void UsageRecordCreate_WithInvalidStatusCode_ReturnsValidationError(int statusCode)
    {
        UsageRecord.Create(
                Guid.NewGuid(), Guid.NewGuid(), null, "embed", TaskKind.Embedding,
                TokenCount.Zero, 10, statusCode, false, TestClock.Now)
            .Error!.Code.Should().Be("usage.status_code.invalid");
    }

    [Theory]
    [InlineData(UsagePeriod.Hour, "2026-09-15T13:00:00+00:00")]
    [InlineData(UsagePeriod.Day, "2026-09-15T00:00:00+00:00")]
    [InlineData(UsagePeriod.Month, "2026-09-01T00:00:00+00:00")]
    public void BucketStartFor_AlignsToPeriodStartInUtc(UsagePeriod period, string expected)
    {
        // 20:47 giờ Việt Nam = 13:47 UTC.
        var at = new DateTimeOffset(2026, 9, 15, 20, 47, 12, TimeSpan.FromHours(7));

        UsageAggregate.BucketStartFor(period, at)
            .Should().Be(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void UsageAggregateAdd_AccumulatesTokensAndRequests()
    {
        var aggregate = UsageAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), UsagePeriod.Day, TestClock.Now).Value;

        aggregate.Add(TokenCount.Create(100, 20, false).Value);
        aggregate.Add(TokenCount.Create(50, 5, true).Value);

        aggregate.PromptTokens.Should().Be(150);
        aggregate.CompletionTokens.Should().Be(25);
        aggregate.RequestCount.Should().Be(2);
    }
}
