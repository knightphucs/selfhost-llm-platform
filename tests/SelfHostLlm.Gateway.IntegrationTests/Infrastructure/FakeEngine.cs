using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

public enum EngineBehavior
{
    JsonWithUsage,
    JsonWithoutUsage,
    SseWithUsage,
    SseWithoutUsage,
}

public sealed record CapturedRequest(string Path, string? Authorization, string Body)
{
    public JsonNode? Json => string.IsNullOrEmpty(Body) ? null : JsonNode.Parse(Body);
}

/// <summary>
/// Engine OpenAI-compatible giả (Kestrel in-process, cổng ngẫu nhiên). Cấu hình được hành vi và
/// ghi lại mọi request nhận được để test kiểm header/body mà gateway forward.
/// </summary>
public sealed class FakeEngine : IAsyncDisposable
{
    public const string CompletionText = "Xin chào từ engine";

    private static readonly string[] StreamPieces = ["Xin chào", " từ", " engine"];
    private static readonly float[] FakeEmbedding = [0.1f, 0.2f, 0.3f];

    private readonly WebApplication _app;

    private FakeEngine(WebApplication app)
    {
        _app = app;
    }

    public Uri BaseUrl { get; private set; } = null!;

    public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

    public EngineBehavior Behavior { get; set; } = EngineBehavior.JsonWithUsage;

    /// <summary>Đặt để engine trả lỗi HTTP này cho mọi request suy luận.</summary>
    public int? FailWithStatus { get; set; }

    public int PromptTokens { get; set; } = 11;

    public int CompletionTokens { get; set; } = 7;

    public static async Task<FakeEngine> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();

        // appsettings.json của Gateway được copy vào thư mục test và có Kestrel:Endpoints (:8080)
        // → bỏ mọi nguồn cấu hình và listen tường minh trên cổng ngẫu nhiên.
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var engine = new FakeEngine(app);

        app.MapGet("/v1/models", (HttpContext http) =>
        {
            engine.Capture(http, "");
            return engine.FailWithStatus is { } status
                ? Results.StatusCode(status)
                : Results.Json(new { @object = "list", data = new[] { new { id = "qwen2.5:7b", @object = "model", owned_by = "fake" } } });
        });
        app.MapPost("/v1/chat/completions", engine.HandleChatAsync);
        app.MapPost("/v1/embeddings", engine.HandleEmbeddingsAsync);

        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        engine.BaseUrl = new Uri(addresses.Addresses.First());
        return engine;
    }

    private async Task HandleChatAsync(HttpContext http)
    {
        var body = await new StreamReader(http.Request.Body).ReadToEndAsync();
        Capture(http, body);
        if (await TryFailAsync(http))
        {
            return;
        }

        var request = JsonNode.Parse(body)!;
        var model = request["model"]?.GetValue<string>() ?? "unknown";
        var usage = new { prompt_tokens = PromptTokens, completion_tokens = CompletionTokens, total_tokens = PromptTokens + CompletionTokens };

        if (Behavior is EngineBehavior.JsonWithUsage or EngineBehavior.JsonWithoutUsage)
        {
            await http.Response.WriteAsJsonAsync(new
            {
                id = "chatcmpl-1",
                @object = "chat.completion",
                created = 1,
                model,
                choices = new[] { new { index = 0, message = new { role = "assistant", content = CompletionText }, finish_reason = "stop" } },
                usage = Behavior == EngineBehavior.JsonWithUsage ? usage : null,
            });
            return;
        }

        http.Response.ContentType = "text/event-stream";
        foreach (var piece in StreamPieces)
        {
            await WriteSseAsync(http, new { id = "c1", @object = "chat.completion.chunk", created = 1, model, choices = new[] { new { index = 0, delta = new { content = piece } } } });
        }

        // Giống vLLM/Ollama: chỉ gửi chunk usage khi client yêu cầu stream_options.include_usage.
        var includeUsage = request["stream_options"]?["include_usage"]?.GetValue<bool>() == true;
        if (Behavior == EngineBehavior.SseWithUsage && includeUsage)
        {
            await WriteSseAsync(http, new { id = "c1", @object = "chat.completion.chunk", created = 1, model, choices = Array.Empty<object>(), usage });
        }

        await http.Response.WriteAsync("data: [DONE]\n\n");
    }

    private async Task HandleEmbeddingsAsync(HttpContext http)
    {
        var body = await new StreamReader(http.Request.Body).ReadToEndAsync();
        Capture(http, body);
        if (await TryFailAsync(http))
        {
            return;
        }

        await http.Response.WriteAsJsonAsync(new
        {
            @object = "list",
            model = JsonNode.Parse(body)!["model"]?.GetValue<string>(),
            data = new[] { new { @object = "embedding", index = 0, embedding = FakeEmbedding } },
            usage = new { prompt_tokens = PromptTokens, total_tokens = PromptTokens },
        });
    }

    private async Task<bool> TryFailAsync(HttpContext http)
    {
        if (FailWithStatus is not { } status)
        {
            return false;
        }

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new { error = new { message = $"engine lỗi {status}", type = "engine_error" } });
        return true;
    }

    private static async Task WriteSseAsync(HttpContext http, object chunk)
    {
        await http.Response.WriteAsync("data: " + JsonSerializer.Serialize(chunk) + "\n\n", Encoding.UTF8);
        await http.Response.Body.FlushAsync();
    }

    private void Capture(HttpContext http, string body) =>
        Requests.Enqueue(new CapturedRequest(http.Request.Path, http.Request.Headers.Authorization.ToString() is { Length: > 0 } a ? a : null, body));

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
