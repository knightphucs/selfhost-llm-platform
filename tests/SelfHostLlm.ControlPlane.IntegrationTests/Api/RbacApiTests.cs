using System.Net;
using System.Net.Http.Json;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Api;

/// <summary>RBAC theo permission + cô lập tenant + chặn leo thang đặc quyền — qua HTTP thật.</summary>
[Collection(PostgresDatabase.Name)]
public sealed class RbacApiTests(PostgresFixture fixture)
{
    private static CreateModelRequest Model(string name = "Qwen2.5-7B") =>
        new(name, "qwen2.5", "7B", "Q4_K_M", 32768, ["Chat"], ["coding"]);

    [Fact]
    public async Task Viewer_CanReadButNotWrite()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (tenantId, viewer) = await app.NewTenantWithUserAsync(SystemRoles.Viewer);

        (await viewer.GetAsync($"/api/v1/tenants/{tenantId}/models")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/models", Model())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operator_CannotManageApiKeysOrUsers()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (tenantId, op) = await app.NewTenantWithUserAsync(SystemRoles.Operator);

        (await op.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/models", Model())).StatusCode.Should().Be(HttpStatusCode.Created);
        (await op.GetAsync($"/api/v1/tenants/{tenantId}/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await op.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/consumers/{Guid.NewGuid()}/api-keys", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_AccessingOtherTenant_Returns403()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (_, adminA) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);
        var (tenantB, _) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);

        var list = await adminA.GetAsync($"/api/v1/tenants/{tenantB}/models");
        var create = await adminA.PostAsJsonAsync($"/api/v1/tenants/{tenantB}/models", Model());

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await list.Content.ReadAsStringAsync()).Should().Contain("tenant_access.forbidden");
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_CannotCreateTenants()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (_, tenantAdmin) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);

        (await tenantAdmin.PostAsJsonAsync("/api/v1/tenants", new CreateTenantRequest("x", "x-tenant")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_GrantingPlatformAdmin_Returns403()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (tenantId, tenantAdmin) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);

        var response = await tenantAdmin.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/users",
            new CreateUserRequest("sneaky-" + Guid.NewGuid().ToString("N")[..6], null, ControlPlaneApp.AdminPassword, [SystemRoles.PlatformAdmin]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("rbac.platform_admin_restricted");
    }

    [Fact]
    public async Task InvalidRequest_Returns400WithFieldErrorsAndCode()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (tenantId, tenantAdmin) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);

        var response = await tenantAdmin.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/models",
            new CreateModelRequest("", "qwen2.5", "7B", "Q4", 0, ["Telepathy"], []));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("validation.failed").And.Contain("\"errors\"").And.Contain("ContextLength");
    }

    [Fact]
    public async Task DuplicateModelName_Returns409()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (tenantId, tenantAdmin) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);
        await tenantAdmin.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/models", Model());

        var response = await tenantAdmin.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/models", Model());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("model.name_taken");
    }
}
