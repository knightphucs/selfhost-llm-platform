using Microsoft.Extensions.Logging.Abstractions;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Routing;

public sealed class FallbackExecutorTests
{
    private readonly FallbackExecutor _executor = new(NullLogger<FallbackExecutor>.Instance, TimeProvider.System);

    private static List<DeploymentEntry> Candidates(int count)
    {
        var b = new RoutingTableBuilder();
        var model = b.Model(Guid.NewGuid(), "Qwen2.5-7B");
        return Enumerable.Range(0, count).Select(_ => b.Deployment(model)).ToList();
    }

    /// <summary>Attempt giả lập: mỗi deployment trả kết quả/ném exception theo kịch bản, ghi lại thứ tự gọi.</summary>
    private static (Func<DeploymentEntry, CancellationToken, Task<AttemptResult<string>>> Attempt, List<Guid> Calls) Script(
        IReadOnlyList<DeploymentEntry> candidates,
        params Func<AttemptResult<string>>[] steps)
    {
        var calls = new List<Guid>();
        return ((deployment, _) =>
        {
            var index = calls.Count;
            calls.Add(deployment.Id);
            return Task.FromResult(steps[index]());
        }, calls);
    }

    [Fact]
    public async Task ExecuteAsync_FirstDeploymentSucceeds_DoesNotFallback()
    {
        var candidates = Candidates(2);
        var (attempt, calls) = Script(candidates, () => AttemptResult<string>.FromStatus(200, "ok"));

        var result = await _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
        result.ServedBy.Should().Be(candidates[0]);
        result.UsedFallback.Should().BeFalse();
        calls.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task ExecuteAsync_5xxThenSuccess_FallsBackToNextDeployment(int status)
    {
        var candidates = Candidates(2);
        var (attempt, calls) = Script(candidates,
            () => AttemptResult<string>.FromStatus(status, "error"),
            () => AttemptResult<string>.FromStatus(200, "ok"));

        var result = await _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        result.Value.Should().Be("ok");
        result.ServedBy.Should().Be(candidates[1]);
        result.UsedFallback.Should().BeTrue();
        result.Attempts.Select(a => a.Outcome).Should().Equal(AttemptOutcome.Transient, AttemptOutcome.Success);
        result.Attempts[0].StatusCode.Should().Be(status);
        calls.Should().Equal(candidates[0].Id, candidates[1].Id);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(413)]
    [InlineData(422)]
    public async Task ExecuteAsync_4xxFromClient_ReturnsImmediatelyWithoutFallback(int status)
    {
        var candidates = Candidates(2);
        var (attempt, calls) = Script(candidates,
            () => AttemptResult<string>.FromStatus(status, "bad request body"),
            () => throw new InvalidOperationException("Deployment thứ hai không được phép bị gọi."));

        var result = await _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("4xx vẫn là response hợp lệ để relay về client");
        result.IsClientError.Should().BeTrue();
        result.Value.Should().Be("bad request body");
        result.UsedFallback.Should().BeFalse();
        calls.Should().Equal(candidates[0].Id);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    public async Task ExecuteAsync_408Or429FromEngine_FallsBack(int status)
    {
        var candidates = Candidates(2);
        var (attempt, _) = Script(candidates,
            () => AttemptResult<string>.FromStatus(status, "busy"),
            () => AttemptResult<string>.FromStatus(200, "ok"));

        var result = await _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        result.ServedBy.Should().Be(candidates[1]);
    }

    public static TheoryData<Exception> TransientExceptions => new()
    {
        new HttpRequestException("Connection refused"),
        new TaskCanceledException("HttpClient.Timeout"),
        new TimeoutException(),
        new IOException("Connection reset"),
        new UpstreamUnavailableException("Circuit is open"),
    };

    [Theory]
    [MemberData(nameof(TransientExceptions))]
    public async Task ExecuteAsync_ConnectionErrorOrTimeout_FallsBack(Exception exception)
    {
        var candidates = Candidates(2);
        var (attempt, _) = Script(candidates,
            () => throw exception,
            () => AttemptResult<string>.FromStatus(200, "ok"));

        var result = await _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        result.ServedBy.Should().Be(candidates[1]);
        result.Attempts[0].Reason.Should().Be(exception.GetType().Name);
    }

    [Fact]
    public async Task ExecuteAsync_AllDeploymentsFail_ReturnsUnavailableWithEveryAttempt()
    {
        var candidates = Candidates(3);
        var (attempt, calls) = Script(candidates,
            () => AttemptResult<string>.FromStatus(503, "x"),
            () => throw new HttpRequestException(),
            () => AttemptResult<string>.FromStatus(500, "x"));

        var result = await _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Unavailable);
        result.Error.Code.Should().Be("routing.all_deployments_failed");
        result.ServedBy.Should().BeNull();
        result.UsedFallback.Should().BeTrue();
        result.Attempts.Should().HaveCount(3).And.OnlyContain(a => a.Outcome == AttemptOutcome.Transient);
        calls.Should().Equal(candidates.Select(c => c.Id));
    }

    [Fact]
    public async Task ExecuteAsync_ClientCancels_StopsWithoutTryingNextDeployment()
    {
        var candidates = Candidates(2);
        using var cts = new CancellationTokenSource();
        var calls = 0;

        var act = () => _executor.ExecuteAsync<string>(candidates, (_, ct) =>
        {
            calls++;
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(AttemptResult<string>.FromStatus(200, "unreachable"));
        }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_NonTransientException_Propagates()
    {
        var candidates = Candidates(2);
        var (attempt, calls) = Script(candidates,
            () => throw new InvalidOperationException("bug trong adapter"),
            () => AttemptResult<string>.FromStatus(200, "ok"));

        var act = () => _executor.ExecuteAsync(candidates, attempt, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_NoCandidates_ReturnsUnavailable()
    {
        var result = await _executor.ExecuteAsync<string>([], (_, _) => throw new InvalidOperationException(), CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Unavailable);
        result.Attempts.Should().BeEmpty();
    }

    [Theory]
    [InlineData(200, AttemptOutcome.Success)]
    [InlineData(302, AttemptOutcome.Success)]
    [InlineData(400, AttemptOutcome.ClientError)]
    [InlineData(404, AttemptOutcome.ClientError)]
    [InlineData(408, AttemptOutcome.Transient)]
    [InlineData(429, AttemptOutcome.Transient)]
    [InlineData(500, AttemptOutcome.Transient)]
    [InlineData(503, AttemptOutcome.Transient)]
    public void UpstreamStatusClassify_MapsStatusToOutcome(int status, AttemptOutcome expected)
    {
        UpstreamStatus.Classify(status).Should().Be(expected);
    }
}
