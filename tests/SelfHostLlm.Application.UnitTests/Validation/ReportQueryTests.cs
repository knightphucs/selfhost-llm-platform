using NSubstitute;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Application.Usage;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Validation;

public sealed class ReportQueryTests
{
    private readonly UseCaseHarness _h = new();
    private static readonly DateTimeOffset From = UseCaseHarness.Now.AddDays(-7);

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task ListUsageRecords_InvalidPage_ReturnsValidation(int page, int pageSize)
    {
        var result = await _h.SendAsync(new ListUsageRecordsQuery(_h.TenantId, null, null, null, null, new PageRequest(page, pageSize)));

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        await _h.UsageQueries.DidNotReceiveWithAnyArgs().ListAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task GetUsageSummary_ToBeforeFrom_ReturnsValidation()
    {
        (await _h.SendAsync(new GetUsageSummaryQuery(_h.TenantId, null, From, From.AddDays(-1))))
            .Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task GetUsageSummary_RangeOverOneYear_ReturnsValidation()
    {
        (await _h.SendAsync(new GetUsageSummaryQuery(_h.TenantId, null, From, From.AddDays(400))))
            .Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task GetUsageSummary_Valid_QueriesOwnTenant()
    {
        var summary = new UsageSummary(null, From, UseCaseHarness.Now, 10, 5, 2, 1, 1);
        _h.UsageQueries.SummaryAsync(_h.TenantId, null, From, UseCaseHarness.Now, Arg.Any<CancellationToken>()).Returns(summary);

        var result = await _h.SendAsync(new GetUsageSummaryQuery(_h.TenantId, null, From, UseCaseHarness.Now));

        result.Value.Should().Be(summary);
    }

    [Fact]
    public async Task ListAuditLogs_OtherTenant_IsForbiddenWithoutQuerying()
    {
        var result = await _h.SendAsync(new ListAuditLogsQuery(Guid.NewGuid(), null, null, null, null, null, new PageRequest()));

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        await _h.AuditLogQueries.DidNotReceiveWithAnyArgs().ListAsync(default, default!, default!, default);
    }
}
