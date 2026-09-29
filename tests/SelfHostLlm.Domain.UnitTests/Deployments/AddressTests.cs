using SelfHostLlm.Domain.Deployments;

namespace SelfHostLlm.Domain.UnitTests.Deployments;

public sealed class AddressTests
{
    [Theory]
    [InlineData("http://192.168.1.50:11434")]
    [InlineData("https://gpu-box.lan:8000/")]
    public void Create_WithAbsoluteHttpUrl_Succeeds(string url)
    {
        var result = Address.Create(url);

        result.IsSuccess.Should().BeTrue();
        result.Value.ToString().Should().NotEndWith("/");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("/v1/models")]
    [InlineData("ftp://192.168.1.50")]
    [InlineData("http://host:8000?x=1")]
    public void Create_WithInvalidUrl_ReturnsValidationError(string? url)
    {
        var result = Address.Create(url);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("deployment.base_url.invalid");
    }

    [Fact]
    public void Create_WithTrailingSlash_IsEqualToWithout()
    {
        Address.Create("http://localhost:11434/").Value
            .Should().Be(Address.Create("http://localhost:11434").Value);
    }
}
