using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.UnitTests.Common;

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.NotFound("model.not_found", "Không tìm thấy model.");

    [Fact]
    public void Success_WithValue_ExposesValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_WithError_ExposesError()
    {
        var result = Result<int>.Failure(SampleError);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SampleError);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperation()
    {
        var result = Result<int>.Failure(SampleError);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>().WithMessage("*model.not_found*");
    }

    [Fact]
    public void ImplicitConversion_FromValueAndError_ProducesMatchingResult()
    {
        Result<string> ok = "hello";
        Result<string> failed = SampleError;
        Result plainFailed = SampleError;

        ok.IsSuccess.Should().BeTrue();
        failed.Error!.Kind.Should().Be(ErrorKind.NotFound);
        plainFailed.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Success_NonGeneric_HasNoError()
    {
        Result.Success().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void FromError_ViaStaticAbstract_CreatesFailureOfExactType()
    {
        static TResult Fail<TResult>(Error error)
            where TResult : IFailureFactory<TResult> => TResult.FromError(error);

        Fail<Result<int>>(SampleError).Error.Should().Be(SampleError);
        Fail<Result>(SampleError).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidationError_WithDetails_KeepsPerFieldMessages()
    {
        var error = Error.Validation("validation.failed", "Dữ liệu không hợp lệ.",
            new Dictionary<string, string[]> { ["name"] = ["Không được rỗng."] });

        error.Details!["name"].Should().Equal("Không được rỗng.");
    }

    [Fact]
    public void RateLimited_HasRateLimitedKind()
    {
        Error.RateLimited("quota.rate_exceeded", "Vượt rate limit.").Kind.Should().Be(ErrorKind.RateLimited);
    }
}
