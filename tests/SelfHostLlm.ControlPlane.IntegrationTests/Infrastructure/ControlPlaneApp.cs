using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;

/// <summary>Control plane thật trong process, trỏ vào Postgres của Testcontainers.</summary>
public sealed class ControlPlaneApp : IAsyncDisposable
{
    public const string InternalToken = "test-internal-token-7d1c";
    public const string AdminPassword = "Adm1n-Passw0rd!";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _keysPath;

    private ControlPlaneApp(WebApplicationFactory<Program> factory, string keysPath)
    {
        _factory = factory;
        _keysPath = keysPath;
    }

    public string AdminUsername { get; private set; } = "";

    public Guid PlatformTenantId { get; private set; }

    public IServiceProvider Services => _factory.Services;

    public static async Task<ControlPlaneApp> StartAsync(
        string connectionString,
        bool seedPlatformAdmin,
        IReadOnlyDictionary<string, string>? extraSettings = null)
    {
        var keysPath = Path.Combine(Path.GetTempPath(), "selfhostllm-cp-keys-" + Guid.NewGuid().ToString("N"));
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // UseSetting được áp trước khi Program đọc cấu hình (khác ConfigureAppConfiguration).
            builder.UseEnvironment("Testing");

            // Validate DI như môi trường Development — bắt lỗi đăng ký thiếu ngay lúc build host.
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateOnBuild = true;
                o.ValidateScopes = true;
            });
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.UseSetting("InternalApi:Token", InternalToken);
            builder.UseSetting("DataProtection:KeysPath", keysPath);
            foreach (var (key, value) in extraSettings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }
        });

        var app = new ControlPlaneApp(factory, keysPath);
        if (seedPlatformAdmin)
        {
            await app.SeedPlatformAdminAsync();
        }

        return app;
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>Đăng nhập, trả client đã gắn bearer token.</summary>
    public async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public Task<HttpClient> LoginAsAdminAsync() => LoginAsync(AdminUsername, AdminPassword);

    /// <summary>Tạo tenant mới + một user có role cho trước (qua API bằng quyền PlatformAdmin), rồi đăng nhập bằng user đó.</summary>
    public async Task<(Guid TenantId, HttpClient Client)> NewTenantWithUserAsync(string role)
    {
        var admin = await LoginAsAdminAsync();
        var slug = "t-" + Guid.NewGuid().ToString("N")[..12];
        var tenant = await (await admin.PostAsJsonAsync("/api/v1/tenants", new CreateTenantRequest("Tenant " + slug, slug)))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<TenantResponse>();

        var username = $"{role.ToLowerInvariant()}-{slug}";
        (await admin.PostAsJsonAsync($"/api/v1/tenants/{tenant!.Id}/users", new CreateUserRequest(username, null, AdminPassword, [role])))
            .EnsureSuccessStatusCode();

        return (tenant.Id, await LoginAsync(username, AdminPassword));
    }

    private async Task SeedPlatformAdminAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantStore>();
        var tenant = Tenant.Create("Platform tests", "p-" + Guid.NewGuid().ToString("N")[..12], DateTimeOffset.UtcNow).Value;
        tenants.Add(tenant);

        AdminUsername = "admin-" + Guid.NewGuid().ToString("N")[..8];
        var created = await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
            .CreateAsync(tenant.Id, AdminUsername, null, AdminPassword, [SystemRoles.PlatformAdmin], CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();
        PlatformTenantId = tenant.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        if (Directory.Exists(_keysPath))
        {
            Directory.Delete(_keysPath, recursive: true);
        }
    }

    public sealed record TokenResponse(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);
}
