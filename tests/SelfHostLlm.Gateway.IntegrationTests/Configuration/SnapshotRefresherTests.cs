using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Gateway.Configuration;
using SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

namespace SelfHostLlm.Gateway.IntegrationTests.Configuration;

/// <summary>Bất biến 1: control plane chết thì Gateway vẫn phục vụ bằng snapshot gần nhất.</summary>
public sealed class SnapshotRefresherTests : IAsyncLifetime
{
    private FakeEngine _engine = null!;
    private readonly string _sharedDir = Path.Combine(Path.GetTempPath(), "selfhostllm-gwshared-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync() => _engine = await FakeEngine.StartAsync();

    private GatewayApp CreateGateway() => GatewayApp.Create(
        keysPath: Path.Combine(_sharedDir, "keys"),
        settings: new Dictionary<string, string> { ["Gateway:SnapshotCachePath"] = Path.Combine(_sharedDir, "snapshot.json") });

    private (SnapshotBuilder Snapshot, Guid DeploymentId) BuildSnapshot(GatewayApp gateway)
    {
        var snapshot = new SnapshotBuilder().WithApiKey();
        var model = snapshot.Model("Qwen2.5-7B");
        var deployment = snapshot.Deployment(model, _engine.BaseUrl, engineKeyCiphertext: gateway.Protect("engine-key-xyz"));
        snapshot.VirtualModel("chat-general", "Chat", deployment);
        return (snapshot, deployment);
    }

    private static async Task<HttpStatusCode> ChatAsync(GatewayApp gateway, string apiKey)
    {
        var client = gateway.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await client.PostAsJsonAsync("/v1/chat/completions",
            new { model = "chat-general", messages = new[] { new { role = "user", content = "ping" } } });
        return response.StatusCode;
    }

    [Fact]
    public async Task Refresh_NotModified_KeepsSameState()
    {
        await using var gateway = CreateGateway();
        var (snapshot, _) = BuildSnapshot(gateway);
        await gateway.ApplyAsync(snapshot.Build());
        var store = gateway.Services.GetRequiredService<GatewayStateStore>();
        var before = store.Current;

        (await gateway.RefreshAsync()).Should().BeTrue();

        store.Current.Should().BeSameAs(before, "304 không dựng lại state");
    }

    [Fact]
    public async Task Refresh_ControlPlaneDown_KeepsServingWithLastSnapshot()
    {
        await using var gateway = CreateGateway();
        var (snapshot, _) = BuildSnapshot(gateway);
        await gateway.ApplyAsync(snapshot.Build());

        gateway.Fetcher.ControlPlaneDown = true;
        (await gateway.RefreshAsync()).Should().BeTrue();

        (await ChatAsync(gateway, snapshot.ApiKey)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Restart_WithControlPlaneDown_LoadsSnapshotFromCacheFile()
    {
        SnapshotBuilder snapshot;
        await using (var first = CreateGateway())
        {
            (snapshot, _) = BuildSnapshot(first);
            await first.ApplyAsync(snapshot.Build());
        }

        _engine.Requests.Clear();
        await using var restarted = CreateGateway();
        restarted.Fetcher.ControlPlaneDown = true;

        (await restarted.RefreshAsync()).Should().BeTrue("snapshot được nạp lại từ file cache");
        (await ChatAsync(restarted, snapshot.ApiKey)).Should().Be(HttpStatusCode.OK);
        _engine.Requests.Should().ContainSingle().Which.Authorization.Should().Be("Bearer engine-key-xyz",
            "engine key trong cache là ciphertext và giải mã được bằng key ring dùng chung");
        (await File.ReadAllTextAsync(Path.Combine(_sharedDir, "snapshot.json"))).Should().NotContain("engine-key-xyz").And.NotContain(snapshot.ApiKey);
    }

    [Fact]
    public async Task Refresh_ControlPlaneDownAndNoCache_IsNotReady()
    {
        await using var gateway = GatewayApp.Create();
        gateway.Fetcher.ControlPlaneDown = true;

        (await gateway.RefreshAsync()).Should().BeFalse();
        (await gateway.CreateClient().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    public async Task DisposeAsync()
    {
        await _engine.DisposeAsync();
        if (Directory.Exists(_sharedDir))
        {
            Directory.Delete(_sharedDir, recursive: true);
        }
    }
}
