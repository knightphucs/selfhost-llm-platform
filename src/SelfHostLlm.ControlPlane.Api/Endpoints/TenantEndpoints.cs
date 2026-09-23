using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Tenants;
using SelfHostLlm.Application.Users;
using SelfHostLlm.ControlPlane.Api.Http;
using SelfHostLlm.ControlPlane.Api.Mapping;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

internal static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var tenants = app.MapGroup("/api/v1/tenants").WithTags("Tenants");

        tenants.MapPost("/", async (CreateTenantRequest body, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.SendAsync(new CreateTenantCommand(body.Name, body.Slug), ct))
                .ToCreated(t => t.ToResponse(), t => $"/api/v1/tenants/{t.Id}"))
            .RequireAuthorization(Permissions.TenantsManage);

        tenants.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.SendAsync(new ListTenantsQuery(), ct)).ToOk(list => list.Select(t => t.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.TenantsManage);

        // Người trong tenant tự xem được tenant của mình — TenantAccessBehavior chặn tenant khác.
        tenants.MapGet("/{tenantId:guid}", async (Guid tenantId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new GetTenantQuery(tenantId), ct)).ToOk(t => t.ToResponse()));

        var users = app.MapGroup(Routes.Tenant + "/users").WithTags("Users").RequireAuthorization(Permissions.RbacManage);

        users.MapGet("/", async (Guid tenantId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ListUsersQuery(tenantId), ct)).ToOk(list => list.Select(u => u.ToResponse()).ToList()));

        users.MapPost("/", async (Guid tenantId, CreateUserRequest body, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new CreateUserCommand(tenantId, body.Username, body.Email, body.Password, body.Roles ?? []), ct))
            .ToCreated(u => u.ToResponse(), u => $"/api/v1/tenants/{tenantId}/users/{u.Id}"));

        users.MapPut("/{userId:guid}/roles", async (Guid tenantId, Guid userId, SetUserRolesRequest body, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SetUserRolesCommand(tenantId, userId, body.Roles ?? []), ct)).ToOk(u => u.ToResponse()));

        return app;
    }
}
