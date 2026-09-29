using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.Domain.UnitTests.Access;

public sealed class QuotaTests
{
    [Fact]
    public void Create_WithAllLimitsNull_MeansUnlimitedAndSucceeds()
    {
        var quota = Quota.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, null);

        quota.IsSuccess.Should().BeTrue();
        quota.Value.TokensPerMinute.Should().BeNull();
    }

    [Theory]
    [InlineData(0, null, null, "quota.tokens_per_minute.not_positive")]
    [InlineData(null, -1L, null, "quota.tokens_per_month.not_positive")]
    [InlineData(null, null, 0, "quota.max_concurrent_requests.not_positive")]
    public void Create_WithNonPositiveLimit_ReturnsValidationError(int? tpm, long? tpmo, int? mcr, string code)
    {
        Quota.Create(Guid.NewGuid(), Guid.NewGuid(), tpm, tpmo, mcr).Error!.Code.Should().Be(code);
    }

    [Fact]
    public void Update_WithInvalidLimit_KeepsPreviousValues()
    {
        var quota = Quota.Create(Guid.NewGuid(), Guid.NewGuid(), 1000, 1_000_000, 4).Value;

        quota.Update(0, 5, 5).IsFailure.Should().BeTrue();

        quota.TokensPerMinute.Should().Be(1000);
        quota.TokensPerMonth.Should().Be(1_000_000);
    }
}
