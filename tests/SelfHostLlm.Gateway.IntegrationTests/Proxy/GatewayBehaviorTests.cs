using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using SelfHostLlm.Gateway.IntegrationTests.Adapters;
using SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

namespace SelfHostLlm.Gateway.IntegrationTests.Proxy;

/// <summary>SSE + usage, quota, cổng đóng, embeddings, /v1/models và cô lập tenant.</summary>
public sealed class GatewayBehaviorTests : IAsyncLifetime
{
    private FakeEngine _engine = null!;
    private FakeEngine _embedEngine = null!;
    private GatewayApp _gateway = null!;

    public async Task InitializeAsync()
    {
        _engine = await FakeEngine.StartAsync();
        _embedEngine = await FakeEngine.StartAsync();
        _gateway = GatewayApp.Create();
    }

    private HttpClient Client(string key)
    {
        var client = _gateway.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private async Task<SnapshotBuilder> ApplyChatSnapshotAsync(Action<SnapshotBuilder>? extra = null, params Uri[] engines)
    {
        var snapshot = new SnapshotBuilder().WithApiKey();
        var model = snapshot.Model("Qwen2.5-7B");
        var deployments = (engines.Length == 0 ? [_engine.BaseUrl] : engines).Select(url => snapshot.Deployment(model, url)).ToArray();
        snapshot.VirtualModel("chat-general", "Chat", deployments);
        extra?.Invoke(snapshot);
        await _gateway.ApplyAsync(snapshot.Build());
        return snapshot;
    }

    private static object Chat(string model = "chat-general", bool stream = false) =>
        new { model, stream, messages = new[] { new { role = "user", content = "Giải thích tenant isolation trong RAG" } } };

    [Fact]
    public async Task Stream_WithUsage_RelaysSseAndReadsUsageFromFinalChunk()
    {
        _engine.Behavior = EngineBehavior.SseWithUsage;
        var snapshot = await ApplyChatSnapshotAsync();

        var response = await Client(snapshot.ApiKey).PostAsJsonAsync("/v1/chat/completions", Chat(stream: true));
        var body = await response.Content.ReadAsStringAsync();

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(5, "3 chunk nội dung + 1 chunk usage + [DONE]");
        body.Should().Contain("chat.completion.chunk").And.EndWith("data: [DONE]\n\n");
        _engine.Requests.Should().ContainSingle().Which.Json!["stream_options"]!["include_usage"]!.GetValue<bool>()
            .Should().BeTrue("gateway tự inject include_usage (QĐ-4)");

        var usage = await _gateway.Usage.WaitForSingleAsync();
        usage.PromptTokens.Should().Be(11);
        usage.CompletionTokens.Should().Be(7);
        usage.TokensEstimated.Should().BeFalse();
    }

    [Fact]
    public async Task Stream_EngineWithoutUsage_EstimatesWithTokenizer()
    {
        _engine.Behavior = EngineBehavior.SseWithoutUsage;
        var snapshot = await ApplyChatSnapshotAsync();

        var response = await Client(snapshot.ApiKey).PostAsJsonAsync("/v1/chat/completions", Chat(stream: true));
        await response.Content.ReadAsStringAsync();

        var usage = await _gateway.Usage.WaitForSingleAsync();
        usage.TokensEstimated.Should().BeTrue();
        usage.PromptTokens.Should().BePositive();
        usage.CompletionTokens.Should().BePositive();
    }

    [Fact]
    public async Task Json_EngineWithoutUsage_EstimatesWithTokenizer()
    {
        _engine.Behavior = EngineBehavior.JsonWithoutUsage;
        var snapshot = await ApplyChatSnapshotAsync();

        (await Client(snapshot.ApiKey).PostAsJsonAsync("/v1/chat/completions", Chat())).EnsureSuccessStatusCode();

        var usage = await _gateway.Usage.WaitForSingleAsync();
        usage.TokensEstimated.Should().BeTrue();
        usage.CompletionTokens.Should().BePositive();
    }

    [Fact]
    public async Task ConnectionRefused_FallsBackToNextDeployment()
    {
        var closed = new Uri($"http://127.0.0.1:{OpenAiCompatibleProviderTests.ClosedPort()}");
        var snapshot = await ApplyChatSnapshotAsync(engines: [closed, _engine.BaseUrl]);

        var response = await Client(snapshot.ApiKey).PostAsJsonAsync("/v1/chat/completions", Chat());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _gateway.Usage.WaitForSingleAsync()).UsedFallback.Should().BeTrue();
    }

    [Fact]
    public async Task Quota_RateExceeded_Returns429WithRetryAfter()
    {
        var snapshot = await ApplyChatSnapshotAsync(s => s.Quota(tokensPerMinute: 10));
        var client = Client(snapshot.ApiKey);

        (await client.PostAsJsonAsync("/v1/chat/completions", Chat())).StatusCode.Should().Be(HttpStatusCode.OK);
        await _gateway.Usage.WaitForSingleAsync();
        var second = await client.PostAsJsonAsync("/v1/chat/completions", Chat());

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.Should().NotBeNull();
        (await second.Content.ReadFromJsonAsync<JsonObject>())!["error"]!["type"]!.GetValue<string>().Should().Be("rate_limit_error");
        _engine.Requests.Should().ContainSingle("request bị chặn quota không được tới engine");
    }

    [Fact]
    public async Task Embeddings_RouteToEmbeddingDeployment()
    {
        var snapshot = new SnapshotBuilder().WithApiKey();
        var bge = snapshot.Model("bge-m3", "Embedding");
        snapshot.VirtualModel("embed", "Embedding", snapshot.Deployment(bge, _embedEngine.BaseUrl, "bge-m3:latest"));
        await _gateway.ApplyAsync(snapshot.Build());

        var response = await Client(snapshot.ApiKey).PostAsJsonAsync("/v1/embeddings", new { model = "embed", input = "đoạn văn cần nhúng" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _embedEngine.Requests.Should().ContainSingle(r => r.Path == "/v1/embeddings").Which.Json!["model"]!.GetValue<string>().Should().Be("bge-m3:latest");
        (await _gateway.Usage.WaitForSingleAsync()).PromptTokens.Should().Be(11);
    }

    [Fact]
    public async Task Embeddings_ToChatVirtualModel_Returns400CapabilityMismatch()
    {
        var snapshot = await ApplyChatSnapshotAsync();

        var response = await Client(snapshot.ApiKey).PostAsJsonAsync("/v1/embeddings", new { model = "chat-general", input = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("routing.capability_mismatch");
    }

    [Fact]
    public async Task OtherTenantVirtualModel_IsNotVisibleOrCallable()
    {
        var snapshot = new SnapshotBuilder().WithApiKey();
        var otherTenant = Guid.NewGuid();
        var otherModel = snapshot.Model("Llama-3.1-8B", tenantId: otherTenant);
        snapshot.VirtualModelFor(otherTenant, "secret-model", "Chat", snapshot.Deployment(otherModel, _engine.BaseUrl, tenantId: otherTenant));
        var ownModel = snapshot.Model("Qwen2.5-7B");
        snapshot.VirtualModel("chat-general", "Chat", snapshot.Deployment(ownModel, _engine.BaseUrl));
        await _gateway.ApplyAsync(snapshot.Build());
        var client = Client(snapshot.ApiKey);

        var call = await client.PostAsJsonAsync("/v1/chat/completions", Chat("secret-model"));
        var models = await client.GetFromJsonAsync<JsonObject>("/v1/models");

        call.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _engine.Requests.Should().BeEmpty();
        models!["data"]!.AsArray().Select(m => m!["id"]!.GetValue<string>()).Should().BeEquivalentTo(["chat-general", "Qwen2.5-7B"]);
    }

    [Fact]
    public async Task Health_ReadyOnlyAfterSnapshot_LiveAlways()
    {
        await using var fresh = GatewayApp.Create();
        var client = fresh.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    public async Task DisposeAsync()
    {
        await _gateway.DisposeAsync();
        await _engine.DisposeAsync();
        await _embedEngine.DisposeAsync();
    }
}
