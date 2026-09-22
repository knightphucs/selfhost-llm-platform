using SelfHostLlm.Domain.Models;

namespace SelfHostLlm.Domain.UnitTests.Models;

public sealed class ModelTests
{
    [Fact]
    public void Create_WithValidInput_NormalizesTaskTags()
    {
        var result = Model.Create(
            Guid.NewGuid(), "Qwen2.5-7B", "qwen2.5", "7B", "Q4_K_M", 32768,
            [ModelCapability.Chat, ModelCapability.Chat], ["Coding", " coding ", ""], TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Capabilities.Should().Equal(ModelCapability.Chat);
        result.Value.TaskTags.Should().Equal("coding");
    }

    [Fact]
    public void Create_WithoutCapabilities_ReturnsValidationError()
    {
        var result = Model.Create(
            Guid.NewGuid(), "Qwen2.5-7B", "qwen2.5", "7B", "Q4_K_M", 32768, [], [], TestClock.Now);

        result.Error!.Code.Should().Be("model.capabilities.empty");
    }

    [Fact]
    public void Create_WithZeroContextLength_ReturnsValidationError()
    {
        var result = Model.Create(
            Guid.NewGuid(), "bge-m3", "bge", "568M", "FP16", 0, [ModelCapability.Embedding], [], TestClock.Now);

        result.Error!.Code.Should().Be("model.context_length.not_positive");
    }
}
