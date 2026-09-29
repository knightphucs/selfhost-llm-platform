using System.Text.Json;
using SelfHostLlm.Contracts.OpenAi;

namespace SelfHostLlm.Domain.UnitTests.Contracts;

public sealed class ChatCompletionRequestJsonTests
{
    private const string SdkRequest = """
        {
          "model": "code-fast",
          "messages": [
            { "role": "system", "content": "Bạn là trợ lý lập trình." },
            { "role": "user", "content": [ { "type": "text", "text": "viết hàm fibonacci" } ] }
          ],
          "stream": true,
          "stop": "###",
          "task": "coding",
          "seed": 7,
          "response_format": { "type": "json_object" }
        }
        """;

    [Fact]
    public void Deserialize_SdkRequest_ReadsRoutingFields()
    {
        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(SdkRequest, OpenAiJson.Options)!;

        request.Model.Should().Be("code-fast");
        request.Stream.Should().BeTrue();
        request.Task.Should().Be("coding");
        request.Stop.Should().Equal("###");
        request.Messages.Should().HaveCount(2);
        request.Messages[1].Content.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void RoundTrip_UnknownFields_ArePreservedViaExtensionData()
    {
        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(SdkRequest, OpenAiJson.Options)!;

        var json = JsonSerializer.Serialize(request, OpenAiJson.Options);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("seed").GetInt32().Should().Be(7);
        doc.RootElement.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
    }

    [Fact]
    public void Serialize_WithStreamOptions_UsesSnakeCaseAndOmitsNulls()
    {
        var request = new ChatCompletionRequest
        {
            Model = "chat-general",
            Messages = [ChatMessage.Text("user", "hi")],
            Stream = true,
            StreamOptions = new StreamOptions { IncludeUsage = true },
        };

        var json = JsonSerializer.Serialize(request, OpenAiJson.Options);

        json.Should().Contain("\"stream_options\":{\"include_usage\":true}")
            .And.NotContain("temperature")
            .And.Contain("\"content\":\"hi\"");
    }

    [Fact]
    public void Deserialize_FinalStreamChunkWithUsage_ReadsUsage()
    {
        const string chunk = """
            {"id":"c1","object":"chat.completion.chunk","created":1,"model":"qwen2.5:7b","choices":[],
             "usage":{"prompt_tokens":12,"completion_tokens":34,"total_tokens":46}}
            """;

        var parsed = JsonSerializer.Deserialize<ChatCompletionChunk>(chunk, OpenAiJson.Options)!;

        parsed.Choices.Should().BeEmpty();
        parsed.Usage!.TotalTokens.Should().Be(46);
    }

    [Fact]
    public void Serialize_ErrorResponse_MatchesOpenAiShape()
    {
        var json = JsonSerializer.Serialize(
            OpenAiErrorResponse.Create("Invalid API key.", "authentication_error", "invalid_api_key"),
            OpenAiJson.Options);

        json.Should().Be("""{"error":{"message":"Invalid API key.","type":"authentication_error","code":"invalid_api_key"}}""");
    }
}
