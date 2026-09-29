using System.Net;
using System.Net.Http.Json;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Contracts.Admin;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Api;

[Collection(PostgresDatabase.Name)]
public sealed class BootstrapTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Startup_OnEmptyMigratedDatabase_CreatesLoginablePlatformAdmin()
    {
        var connectionString = await fixture.CreateFreshDatabaseAsync();
        var settings = new Dictionary<string, string>
        {
            ["Bootstrap:AdminUsername"] = "root",
            ["Bootstrap:AdminPassword"] = ControlPlaneApp.AdminPassword,
        };

        await using (var first = await ControlPlaneApp.StartAsync(connectionString, seedPlatformAdmin: false, settings))
        {
            var client = await first.LoginAsync("root", ControlPlaneApp.AdminPassword);
            (await client.GetFromJsonAsync<MeResponse>("/api/v1/auth/me"))!.Roles.Should().Equal("PlatformAdmin");
        }

        // Khởi động lại với mật khẩu khác: đã có user nên bootstrap không làm gì — mật khẩu cũ vẫn đúng.
        settings["Bootstrap:AdminPassword"] = "Other-Passw0rd!";
        await using var second = await ControlPlaneApp.StartAsync(connectionString, seedPlatformAdmin: false, settings);
        var response = await second.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("root", ControlPlaneApp.AdminPassword));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
