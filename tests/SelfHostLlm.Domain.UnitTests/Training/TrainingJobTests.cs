using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Training;

namespace SelfHostLlm.Domain.UnitTests.Training;

public sealed class TrainingJobTests
{
    private static TrainingJob NewJob() =>
        TrainingJob.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TrainingMethod.QLoRA,
            new Dictionary<string, string> { ["r"] = "16", ["epochs"] = "1" }).Value;

    [Fact]
    public void Create_NewJob_IsQueued()
    {
        var job = NewJob();

        job.Status.Should().Be(TrainingJobStatus.Queued);
        job.Hyperparameters.Should().ContainKey("r");
    }

    [Fact]
    public void StartThenSucceed_FromQueued_EndsSucceededWithTimestamps()
    {
        var job = NewJob();

        job.Start(TestClock.Now, "file:///ml/outputs/job.log").IsSuccess.Should().BeTrue();
        job.Succeed(TestClock.Now.AddHours(1)).IsSuccess.Should().BeTrue();

        job.Status.Should().Be(TrainingJobStatus.Succeeded);
        job.StartedAt.Should().Be(TestClock.Now);
        job.FinishedAt.Should().Be(TestClock.Now.AddHours(1));
    }

    [Fact]
    public void Fail_FromRunning_EndsFailed()
    {
        var job = NewJob();
        job.Start(TestClock.Now, null);

        job.Fail(TestClock.Now).IsSuccess.Should().BeTrue();
        job.Status.Should().Be(TrainingJobStatus.Failed);
    }

    [Fact]
    public void Succeed_FromQueued_ReturnsConflict()
    {
        var job = NewJob();

        job.Succeed(TestClock.Now).Error!.Kind.Should().Be(ErrorKind.Conflict);
        job.Status.Should().Be(TrainingJobStatus.Queued);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ReturnsConflict()
    {
        var job = NewJob();
        job.Start(TestClock.Now, null);

        job.Start(TestClock.Now, null).Error!.Code.Should().Be("training_job.invalid_transition");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_FromQueuedOrRunning_EndsCancelled(bool startFirst)
    {
        var job = NewJob();
        if (startFirst)
        {
            job.Start(TestClock.Now, null);
        }

        job.Cancel(TestClock.Now).IsSuccess.Should().BeTrue();
        job.Status.Should().Be(TrainingJobStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AfterSucceeded_ReturnsConflict()
    {
        var job = NewJob();
        job.Start(TestClock.Now, null);
        job.Succeed(TestClock.Now);

        job.Cancel(TestClock.Now).Error!.Kind.Should().Be(ErrorKind.Conflict);
        job.Status.Should().Be(TrainingJobStatus.Succeeded);
    }
}
