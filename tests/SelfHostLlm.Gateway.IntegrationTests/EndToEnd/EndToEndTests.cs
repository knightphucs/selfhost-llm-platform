extern alias controlplane;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Tenancy;
using SelfHostLlm.Gateway.Configuration;
using SelfHostLlm.Gateway.IntegrationTests.Infrastructure;
using ControlPlaneProgram = controlplane::Program;

namespace SelfHostLlm.Gateway.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end GĐ0: admin cấu hình qua Control plane → Gateway kéo snapshot qua HTTP → client gọi
/// <c>/v1/chat/completions</c> → engine trả lời → usage được ghi vào Postgres (đọc lại qua API CP).
/// Sau đó TẮT control plane: Gateway vẫn phục vụ (bất biến 1) và vẫn ghi usage (QĐ-2).
/// </summary>
[Collection(PostgresDatabase.CollectionName)]
public sealed class EndToEndTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string InternalToken = "e2e-internal-token";
    private const string AdminPassword = "Adm1n-Passw0rd!";
    private const string EngineKey = "engine-secret-e2e";

    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), "selfhostllm-e2e-keys-" + Guid.NewGuid().ToString("N"));
    private FakeEngine _engine = null!;
    private WebApplicationFactory<ControlPlaneProgram> _controlPlane = null!;

    public async Task InitializeAsync()
    {
        _engine = await FakeEngine.StartAsync();
        _controlPlane = new WebApplicationFactory<ControlPlaneProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            // Validate DI như môi trường Development — bắt lỗi đăng ký thiếu ngay lúc build host.
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateOnBuild = true;
                o.ValidateScopes = true;
            });
            builder.UseSetting("ConnectionStrings:Postgres", database.ConnectionString);
            builder.UseSetting("InternalApi:Token", InternalToken);
            builder.UseSetting("DataProtection:KeysPath", _keysPath);
        });
    }

    [Fact]
    public async Task AdminConfigures_ClientChats_UsageRecorded_AndGatewaySurvivesControlPlaneShutdown()
    {
        // 1. Admin (PlatformAdmin) cấu hình tenant qua API của control plane.
        var (tenantId, admin) = await SeedAdminAsync();
        var api = $"/api/v1/tenants/{tenantId}";
        var model = await Read<ModelResponse>(await admin.PostAsJsonAsync($"{api}/models",
            new CreateModelRequest("Qwen2.5-7B", "qwen2.5", "7B", "Q4_K_M", 32768, ["Chat"], ["coding"])));
        var provider = await Read<ProviderResponse>(await admin.PostAsJsonAsync($"{api}/providers", new CreateProviderRequest("Ollama PC", "Ollama", null)));
        var deployment = await Read<DeploymentResponse>(await admin.PostAsJsonAsync($"{api}/deployments",
            new CreateDeploymentRequest(model.Id, null, provider.Id, _engine.BaseUrl.ToString(), "qwen2.5:7b-instruct-q4_K_M", EngineKey)));
        deployment.HealthStatus.Should().Be("Healthy", "control plane probe engine ngay khi đăng ký");
        var virtualModel = await Read<VirtualModelResponse>(await admin.PostAsJsonAsync($"{api}/virtual-models", new CreateVirtualModelRequest("code-fast", "Coding", null)));
        await Read<RouteResponse>(await admin.PostAsJsonAsync($"{api}/virtual-models/{virtualModel.Id}/routes", new CreateRouteRequest(virtualModel.Id, deployment.Id, 0)));
        var consumer = await Read<ConsumerResponse>(await admin.PostAsJsonAsync($"{api}/consumers", new CreateConsumerRequest("IDE plugin", null)));
        var apiKey = await Read<CreateApiKeyResponse>(await admin.PostAsJsonAsync($"{api}/consumers/{consumer.Id}/api-keys", new CreateApiKeyRequest(consumer.Id, null)));

        // 2. Gateway kéo snapshot từ control plane qua HTTP (token nội bộ + ETag).
        var link = new ControlPlaneLink(_controlPlane.Server.CreateHandler());
        await using var gateway = GatewayApp.Create(
            connectionString: database.ConnectionString,
            replaceUsageSink: false,
            keysPath: _keysPath,
            configureServices: services => services.AddHttpClient(HttpConfigSnapshotFetcher.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => link),
            settings: new Dictionary<string, string>
            {
                ["ControlPlane:BaseUrl"] = "http://localhost",
                ["ControlPlane:InternalToken"] = InternalToken,
            });
        (await gateway.RefreshAsync()).Should().BeTrue();

        // 3. Client gọi chat bằng API key vừa cấp.
        var client = gateway.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Key);
        var chat = new { model = "code-fast", messages = new[] { new { role = "user", content = "Viết hàm fibonacci" } } };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", chat);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forwarded = _engine.Requests.Last(r => r.Path == "/v1/chat/completions");
        forwarded.Json!["model"]!.GetValue<string>().Should().Be("qwen2.5:7b-instruct-q4_K_M");
        forwarded.Authorization.Should().Be($"Bearer {EngineKey}", "Gateway giải mã engine key bằng key ring dùng chung với CP");

        // 4. Usage được Gateway ghi thẳng vào Postgres — đọc lại qua API usage của control plane.
        var summary = await WaitForUsageAsync(admin, api, expectedRequests: 1);
        summary["promptTokens"]!.GetValue<long>().Should().Be(11);
        summary["completionTokens"]!.GetValue<long>().Should().Be(7);

        // 5. TẮT control plane. Gateway vẫn phục vụ bằng snapshot gần nhất và vẫn ghi usage.
        link.Down = true;
        await _controlPlane.DisposeAsync();
        (await gateway.RefreshAsync()).Should().BeTrue("mất control plane thì giữ snapshot cũ");

        (await client.PostAsJsonAsync("/v1/chat/completions", chat)).StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForUsageRowsAsync(tenantId, expected: 2);
    }

    /// <summary>Đường mạng Gateway → CP. <c>Down</c> mô phỏng CP chết: kết nối bị từ chối như ngoài đời.</summary>
    private sealed class ControlPlaneLink(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public bool Down { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Down
                ? throw new HttpRequestException("Connection refused (control plane down)")
                : base.SendAsync(request, cancellationToken);
    }

    private async Task<(Guid TenantId, HttpClient Admin)> SeedAdminAsync()
    {
        await using var scope = _controlPlane.Services.CreateAsyncScope();
        var tenant = Tenant.Create("E2E", "e2e-" + Guid.NewGuid().ToString("N")[..10], DateTimeOffset.UtcNow).Value;
        scope.ServiceProvider.GetRequiredService<ITenantStore>().Add(tenant);
        var username = "e2e-admin-" + Guid.NewGuid().ToString("N")[..6];
        (await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
            .CreateAsync(tenant.Id, username, null, AdminPassword, [SystemRoles.PlatformAdmin], CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();

        var client = _controlPlane.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, AdminPassword)))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonObject>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!["accessToken"]!.GetValue<string>());
        return (tenant.Id, client);
    }

    private static async Task<JsonObject> WaitForUsageAsync(HttpClient admin, string api, int expectedRequests)
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        for (var i = 0; i < 50; i++)
        {
            var summary = await admin.GetFromJsonAsync<JsonObject>($"{api}/usage/summary?from={from}&to={to}");
            if (summary!["requestCount"]!.GetValue<long>() >= expectedRequests)
            {
                return summary;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Usage chưa được ghi vào DB (UsageFlushService batch tối đa 2 giây).");
    }

    private async Task WaitForUsageRowsAsync(Guid tenantId, int expected)
    {
        await using var dataSource = SelfHostLlm.Persistence.AppDbContextOptions.BuildDataSource(database.ConnectionString);
        for (var i = 0; i < 50; i++)
        {
            await using var db = PostgresDatabase.CreateDbContext(dataSource);
            if (db.UsageRecords.Count(u => u.TenantId == tenantId) >= expected)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Gateway không ghi được usage sau khi control plane tắt.");
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public async Task DisposeAsync()
    {
        await _controlPlane.DisposeAsync();
        await _engine.DisposeAsync();
        if (Directory.Exists(_keysPath))
        {
            Directory.Delete(_keysPath, recursive: true);
        }
    }
}
