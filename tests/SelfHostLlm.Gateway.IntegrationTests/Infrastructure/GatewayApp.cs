using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Contracts.Internal;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Gateway.Configuration;
using SelfHostLlm.Gateway.Routing;
using Yarp.ReverseProxy;

namespace SelfHostLlm.Gateway.IntegrationTests.Infrastructure;

/// <summary>Gateway thật trong process với nguồn snapshot tĩnh và usage sink bắt bản ghi.</summary>
public sealed class GatewayApp : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _workDir;

    private GatewayApp(WebApplicationFactory<Program> factory, string workDir)
    {
        _factory = factory;
        _workDir = workDir;
    }

    internal StaticSnapshotFetcher Fetcher { get; } = new();

    public CapturingUsageSink Usage { get; } = new();

    public IServiceProvider Services => _factory.Services;

    public string CachePath => Path.Combine(_workDir, "snapshot.json");

    public static GatewayApp Create(
        string? connectionString = null,
        bool replaceUsageSink = true,
        string? keysPath = null,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "selfhostllm-gw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        GatewayApp? app = null;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            // Validate DI như môi trường Development — bắt lỗi đăng ký thiếu ngay lúc build host.
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateOnBuild = true;
                o.ValidateScopes = true;
            });
            builder.UseSetting("ConnectionStrings:Postgres", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none");
            builder.UseSetting("DataProtection:KeysPath", keysPath ?? Path.Combine(workDir, "keys"));
            builder.UseSetting("Gateway:SnapshotCachePath", Path.Combine(workDir, "snapshot.json"));
            builder.UseSetting("Gateway:SnapshotRefreshSeconds", "3600");
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                if (configureServices is null)
                {
                    services.RemoveAll<IConfigSnapshotFetcher>();
                    services.AddSingleton<IConfigSnapshotFetcher>(_ => app!.Fetcher);
                }

                if (replaceUsageSink)
                {
                    services.RemoveAll<IUsageSink>();
                    services.AddSingleton<IUsageSink>(_ => app!.Usage);
                }

                configureServices?.Invoke(services);
            });
        });

        app = new GatewayApp(factory, workDir);
        return app;
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>Đẩy snapshot vào gateway và chờ YARP nạp xong cluster của mọi deployment.</summary>
    public async Task ApplyAsync(ConfigSnapshotDto snapshot)
    {
        Fetcher.Next = snapshot;
        _ = _factory.Server; // đảm bảo host đã khởi động
        (await Services.GetRequiredService<SnapshotRefresher>().RefreshAsync(CancellationToken.None)).Should().BeTrue();
        await WaitForClustersAsync(snapshot.Deployments.Select(d => d.Id));
    }

    public async Task WaitForClustersAsync(IEnumerable<Guid> deploymentIds)
    {
        var lookup = Services.GetRequiredService<IProxyStateLookup>();
        var ids = deploymentIds.ToList();
        for (var i = 0; i < 100; i++)
        {
            if (ids.All(id => lookup.TryGetCluster(SnapshotProxyConfigProvider.ClusterIdFor(id), out _)))
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("YARP chưa nạp cluster của snapshot.");
    }

    /// <summary>Làm mới cấu hình bằng nguồn thật (HTTP tới control plane) rồi chờ YARP nạp cluster.</summary>
    public async Task<bool> RefreshAsync()
    {
        _ = _factory.Server;
        var refreshed = await Services.GetRequiredService<SnapshotRefresher>().RefreshAsync(CancellationToken.None);
        var state = Services.GetRequiredService<GatewayStateStore>().Current;
        if (state is not null)
        {
            await WaitForClustersAsync(state.Deployments.Select(d => d.Id));
        }

        return refreshed;
    }

    public string Protect(string plaintext) => Services.GetRequiredService<ISecretProtector>().Protect(plaintext);

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        try
        {
            Directory.Delete(_workDir, recursive: true);
        }
        catch (IOException)
        {
            // Thư mục tạm — bỏ qua.
        }
    }
}

internal sealed class StaticSnapshotFetcher : IConfigSnapshotFetcher
{
    private string? _servedETag;

    public ConfigSnapshotDto? Next { get; set; }

    /// <summary>Đặt true để mô phỏng control plane chết.</summary>
    public bool ControlPlaneDown { get; set; }

    public Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken)
    {
        if (ControlPlaneDown || Next is null)
        {
            return Task.FromResult<FetchResult>(new FetchResult.Failed("control plane down"));
        }

        var nextETag = "\"" + Next.GetHashCode().ToString("x", System.Globalization.CultureInfo.InvariantCulture) + "\"";
        if (etag == nextETag && _servedETag == nextETag)
        {
            return Task.FromResult<FetchResult>(new FetchResult.NotModified());
        }

        _servedETag = nextETag;
        return Task.FromResult<FetchResult>(new FetchResult.Updated(Next, nextETag));
    }
}

public sealed class CapturingUsageSink : IUsageSink
{
    public ConcurrentQueue<UsageRecord> Records { get; } = new();

    public bool TryWrite(UsageRecord record)
    {
        Records.Enqueue(record);
        return true;
    }

    /// <summary>Usage ghi sau khi response đã trả — chờ tối đa 2 giây.</summary>
    public async Task<UsageRecord> WaitForSingleAsync()
    {
        for (var i = 0; i < 100 && Records.IsEmpty; i++)
        {
            await Task.Delay(20);
        }

        return Records.Should().ContainSingle().Subject;
    }
}
