using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Api;

[Collection(PostgresDatabase.Name)]
public sealed class AuthApiTests(PostgresFixture fixture)
{
    [Fact]
    public async Task HealthLive_WithoutToken_Returns200()
    {
        var app = await fixture.GetControlPlaneAsync();

        (await app.CreateClient().GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/tenants")]
    public async Task Api_WithoutToken_Returns401NotRedirect(string path)
    {
        var app = await fixture.GetControlPlaneAsync();

        var response = await app.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithCode()
    {
        var app = await fixture.GetControlPlaneAsync();

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(app.AdminUsername, "wrong-password-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_PlatformAdmin_MeReturnsRoleAndAllPermissions()
    {
        var app = await fixture.GetControlPlaneAsync();
        var client = await app.LoginAsAdminAsync();

        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/auth/me");

        me!.Roles.Should().Equal(SystemRoles.PlatformAdmin);
        me.TenantId.Should().Be(app.PlatformTenantId);
        me.Permissions.Should().BeEquivalentTo(Permissions.All);
    }

    [Fact]
    public async Task Refresh_WithRefreshToken_ReturnsNewAccessToken()
    {
        var app = await fixture.GetControlPlaneAsync();
        var client = app.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(app.AdminUsername, ControlPlaneApp.AdminPassword)))
            .Content.ReadFromJsonAsync<ControlPlaneApp.TokenResponse>();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(login!.RefreshToken));

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await refreshed.Content.ReadFromJsonAsync<ControlPlaneApp.TokenResponse>())!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("/register")]
    [InlineData("/api/v1/auth/register")]
    public async Task SelfRegistration_EndpointDoesNotExist(string path)
    {
        var app = await fixture.GetControlPlaneAsync();
        var client = await app.LoginAsAdminAsync();

        var response = await client.PostAsJsonAsync(path, new { email = "x@y.z", password = "Whatever-123!" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_IsAudited()
    {
        var app = await fixture.GetControlPlaneAsync();
        var client = await app.LoginAsAdminAsync();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tenants/{app.PlatformTenantId}/audit-logs?entityType=AppUser");

        page.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("action").GetString()).Should().Contain("Login");
    }
}
