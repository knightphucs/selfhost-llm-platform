using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Domain.UnitTests.Routing;

public sealed class RoutingEntityTests
{
    [Theory]
    [InlineData("code-fast")]
    [InlineData("embed")]
    public void VirtualModelCreate_WithValidName_Succeeds(string name)
    {
        VirtualModel.Create(Guid.NewGuid(), name, TaskKind.Coding, null).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Code-Fast")]
    [InlineData("-code")]
    [InlineData("a")]
    [InlineData("code fast")]
    public void VirtualModelCreate_WithInvalidName_ReturnsValidationError(string name)
    {
        VirtualModel.Create(Guid.NewGuid(), name, TaskKind.Coding, null)
            .Error!.Code.Should().Be("virtual_model.name.invalid");
    }

    [Fact]
    public void RouteCreate_WithNegativePriority_ReturnsValidationError()
    {
        Route.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), priority: -1)
            .Error!.Code.Should().Be("route.priority.negative");
    }

    [Fact]
    public void RouteCreate_WithZeroWeight_ReturnsValidationError()
    {
        Route.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), priority: 0, weight: 0)
            .Error!.Code.Should().Be("route.weight.not_positive");
    }
}
