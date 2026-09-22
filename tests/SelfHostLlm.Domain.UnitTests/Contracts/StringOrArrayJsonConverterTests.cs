using System.Text.Json;
using SelfHostLlm.Contracts.OpenAi;

namespace SelfHostLlm.Domain.UnitTests.Contracts;

public sealed class StringOrArrayJsonConverterTests
{
    [Fact]
    public void Deserialize_InputAsSingleString_ReturnsOneItem()
    {
        var request = JsonSerializer.Deserialize<EmbeddingsRequest>(
            """{"model":"embed","input":"xin chào"}""", OpenAiJson.Options)!;

        request.Input.Should().Equal("xin chào");
    }

    [Fact]
    public void Deserialize_InputAsArray_ReturnsAllItems()
    {
        var request = JsonSerializer.Deserialize<EmbeddingsRequest>(
            """{"model":"embed","input":["a","b","c"]}""", OpenAiJson.Options)!;

        request.Input.Should().Equal("a", "b", "c");
    }

    [Theory]
    [InlineData("""{"model":"embed","input":42}""")]
    [InlineData("""{"model":"embed","input":["a",1]}""")]
    public void Deserialize_InputWithNonString_ThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<EmbeddingsRequest>(json, OpenAiJson.Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_Input_WritesArray()
    {
        var json = JsonSerializer.Serialize(
            new EmbeddingsRequest { Model = "embed", Input = ["x"] }, OpenAiJson.Options);

        json.Should().Contain("\"input\":[\"x\"]");
    }
}
