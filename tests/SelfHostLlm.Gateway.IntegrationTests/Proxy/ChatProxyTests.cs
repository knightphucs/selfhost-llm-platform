using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using SelfHostLlm.Gateway.IntegrationTests.Adapters;
using SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

namespace SelfHostLlm.Gateway.IntegrationTests.Proxy;

/// <summary>Routing + fallback + metering qua YARP full pipeline, với engine giả thật (HTTP).</summary>
public sealed class ChatProxyTests : IAsyncLifetime
{
    private const string EngineKey = "engine-secret-abc";

    private FakeEngine _primary = null!;
    private FakeEngine _secondary = null!;
    private GatewayApp _gateway = null!;
    private SnapshotBuilder _snapshot = null!;
    private Guid _primaryId;
    private Guid _secondaryId;

    public async Task InitializeAsync()
    {
        _primary = await FakeEngine.StartAsync();
        _secondary = await FakeEngine.StartAsync();
        _gateway = GatewayApp.Create();
        _snapshot = new SnapshotBuilder().WithApiKey();
        var model = _snapshot.Model("Qwen2.5-7B");
        _primaryId = _snapshot.Deployment(model, _primary.BaseUrl, "qwen-remote-primary", _gateway.Protect(EngineKey));
        _secondaryId = _snapshot.Deployment(model, _secondary.BaseUrl, "qwen-remote-secondary");
        _snapshot.VirtualModel("code-fast", "Coding", _primaryId, _secondaryId);
        await _gateway.ApplyAsync(_snapshot.Build());
    }

    private HttpClient Client(string? key = null)
    {
        var client = _gateway.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key ?? _snapshot.ApiKey);
        return client;
    }

    private static object Chat(string model = "code-fast", bool stream = false) => new
    {
        model,
        stream,
        messages = new[] { new { role = "user", content = "Viết hàm fibonacci" } },
    };

    [Fact]
    public async Task Chat_WithoutApiKey_Returns401InOpenAiFormat()
    {
        var response = await _gateway.CreateClient().PostAsJsonAsync("/v1/chat/completions", Chat());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();
        error!["error"]!["type"]!.GetValue<string>().Should().Be("authentication_error");
        _primary.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Chat_WithUnknownKey_Returns401()
    {
        var response = await Client("sk-" + new string('A', 43)).PostAsJsonAsync("/v1/chat/completions", Chat());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chat_RoutesToPriorityDeployment_RewritesModelAndSwapsCredentials()
    {
        var response = await Client().PostAsJsonAsync("/v1/chat/completions", Chat());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(FakeEngine.CompletionText);

        var forwarded = _primary.Requests.Should().ContainSingle().Subject;
        forwarded.Json!["model"]!.GetValue<string>().Should().Be("qwen-remote-primary");
        forwarded.Json["task"].Should().BeNull();
        forwarded.Authorization.Should().Be($"Bearer {EngineKey}", "engine nhận key của engine...");
        forwarded.Authorization.Should().NotContain(_snapshot.ApiKey, "...và KHÔNG BAO GIỜ nhận key của client");
        _secondary.Requests.Should().BeEmpty();

        var usage = await _gateway.Usage.WaitForSingleAsync();
        usage.DeploymentId.Should().Be(_primaryId);
        usage.PromptTokens.Should().Be(11);
        usage.CompletionTokens.Should().Be(7);
        usage.TokensEstimated.Should().BeFalse();
        usage.UsedFallback.Should().BeFalse();
        usage.RequestedModel.Should().Be("code-fast");
        usage.ApiKeyId.Should().Be(_snapshot.ApiKeyId);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(429)]
    [InlineData(408)]
    public async Task Chat_PrimaryTransientError_FallsBackBeforeFirstByte(int status)
    {
        _primary.FailWithStatus = status;

        var response = await Client().PostAsJsonAsync("/v1/chat/completions", Chat());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(FakeEngine.CompletionText).And.NotContain("engine lỗi", "body lỗi của engine 1 không được lọt tới client");
        _primary.Requests.Should().ContainSingle();
        _secondary.Requests.Should().ContainSingle().Which.Authorization.Should().BeNull("deployment 2 không có engine key");

        var usage = await _gateway.Usage.WaitForSingleAsync();
        usage.DeploymentId.Should().Be(_secondaryId);
        usage.UsedFallback.Should().BeTrue();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(422)]
    public async Task Chat_PrimaryClientError_IsReturnedWithoutFallback(int status)
    {
        _primary.FailWithStatus = status;

        var response = await Client().PostAsJsonAsync("/v1/chat/completions", Chat());

        ((int)response.StatusCode).Should().Be(status);
        (await response.Content.ReadAsStringAsync()).Should().Contain("engine lỗi");
        _secondary.Requests.Should().BeEmpty();
        (await _gateway.Usage.WaitForSingleAsync()).UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Chat_AllDeploymentsFail_Returns503InOpenAiFormat()
    {
        _primary.FailWithStatus = 503;
        _secondary.FailWithStatus = 502;

        var response = await Client().PostAsJsonAsync("/v1/chat/completions", Chat());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();
        error!["error"]!["code"]!.GetValue<string>().Should().Be("routing.all_deployments_failed");

        var usage = await _gateway.Usage.WaitForSingleAsync();
        usage.DeploymentId.Should().BeNull();
        usage.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Chat_UnknownModel_Returns404ModelNotFound()
    {
        var response = await Client().PostAsJsonAsync("/v1/chat/completions", Chat("gpt-4o"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("routing.model_not_found");
    }

    [Fact]
    public async Task Chat_InvalidJson_Returns400()
    {
        var response = await Client().PostAsync("/v1/chat/completions", new StringContent("not json", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public async Task DisposeAsync()
    {
        await _gateway.DisposeAsync();
        await _primary.DisposeAsync();
        await _secondary.DisposeAsync();
    }
}
