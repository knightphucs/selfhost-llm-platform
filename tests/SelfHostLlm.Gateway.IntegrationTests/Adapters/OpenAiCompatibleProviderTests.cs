using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Adapters.Inference;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

namespace SelfHostLlm.Gateway.IntegrationTests.Adapters;

public sealed class OpenAiCompatibleProviderTests : IAsyncLifetime
{
    private FakeEngine _engine = null!;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        _engine = await FakeEngine.StartAsync();
        _services = new ServiceCollection().AddInferenceAdapters().BuildServiceProvider();
    }

    private IInferenceProvider Provider => _services.GetRequiredService<IInferenceProviderFactory>().For(ProviderKind.Ollama);

    [Fact]
    public async Task ProbeAsync_HealthyEngine_ReturnsHealthyWithLatencyAndSendsBearer()
    {
        var result = await Provider.ProbeAsync(new InferenceEndpoint(_engine.BaseUrl, "engine-key-1"), CancellationToken.None);

        result.Healthy.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
        _engine.Requests.Should().ContainSingle(r => r.Path == "/v1/models").Which.Authorization.Should().Be("Bearer engine-key-1");
    }

    [Fact]
    public async Task ProbeAsync_WithoutKey_SendsNoAuthorization()
    {
        await Provider.ProbeAsync(new InferenceEndpoint(_engine.BaseUrl, null), CancellationToken.None);

        _engine.Requests.Should().ContainSingle().Which.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_Engine500_ReturnsUnhealthyWithStatus()
    {
        _engine.FailWithStatus = 500;

        var result = await Provider.ProbeAsync(new InferenceEndpoint(_engine.BaseUrl, null), CancellationToken.None);

        result.Healthy.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task ProbeAsync_ConnectionRefused_ReturnsUnhealthyWithoutThrowing()
    {
        var result = await Provider.ProbeAsync(new InferenceEndpoint(new Uri($"http://127.0.0.1:{ClosedPort()}"), null), CancellationToken.None);

        result.Healthy.Should().BeFalse();
        result.Error.Should().Be(nameof(HttpRequestException));
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsEngineModelIds()
    {
        (await Provider.ListModelsAsync(new InferenceEndpoint(_engine.BaseUrl, null), CancellationToken.None))
            .Should().Equal("qwen2.5:7b");
    }

    [Fact]
    public void TokenCounter_CountsDeterministically()
    {
        var counter = _services.GetRequiredService<ITokenCounter>();

        var count = counter.Count("Viết hàm fibonacci bằng C#");

        count.Should().BePositive().And.Be(counter.Count("Viết hàm fibonacci bằng C#"));
        counter.Count("").Should().Be(0);
    }

    /// <summary>Lấy một cổng trống rồi đóng lại — kết nối tới đó sẽ bị từ chối ngay.</summary>
    internal static int ClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _engine.DisposeAsync();
    }
}
