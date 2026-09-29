using SelfHostLlm.Application.Access;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.ControlPlane.Api.Http;
using SelfHostLlm.ControlPlane.Api.Mapping;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

/// <summary>Consumer, API key (plaintext trả đúng một lần lúc tạo), quota.</summary>
internal static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var consumers = app.MapGroup(Routes.Tenant + "/consumers").WithTags("Access");

        consumers.MapGet("/", async (Guid tenantId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new ListConsumersQuery(tenantId), ct)).ToOk(list => list.Select(c => c.ToResponse()).ToList()))
            .RequireAuthorization(Permissions.ConsumersRead);

        consumers.MapGet("/{consumerId:guid}", async (Guid tenantId, Guid consumerId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetConsumerQuery(tenantId, consumerId), ct)).ToOk(c => c.ToResponse()))
            .RequireAuthorization(Permissions.ConsumersRead);

        consumers.MapPost("/", async (Guid tenantId, CreateConsumerRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateConsumerCommand(tenantId, b.Name, b.Description), ct))
                .ToCreated(c => c.ToResponse(), c => $"/api/v1/tenants/{tenantId}/consumers/{c.Id}"))
            .RequireAuthorization(Permissions.ConsumersWrite);

        consumers.MapPut("/{consumerId:guid}", async (Guid tenantId, Guid consumerId, UpdateConsumerRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpdateConsumerCommand(tenantId, consumerId, b.Name, b.Description, b.Enabled), ct)).ToOk(c => c.ToResponse()))
            .RequireAuthorization(Permissions.ConsumersWrite);

        consumers.MapGet("/{consumerId:guid}/api-keys", async (Guid tenantId, Guid consumerId, IDispatcher d, TimeProvider clock, CancellationToken ct) =>
                (await d.SendAsync(new ListApiKeysQuery(tenantId, consumerId), ct)).ToOk(list => list.Select(k => k.ToResponse(clock.GetUtcNow())).ToList()))
            .RequireAuthorization(Permissions.ApiKeysManage);

        consumers.MapPost("/{consumerId:guid}/api-keys", async (Guid tenantId, Guid consumerId, CreateApiKeyRequest? b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new CreateApiKeyCommand(tenantId, consumerId, b?.ExpiresAt), ct))
                .ToCreated(k => k.ToResponse(), k => $"/api/v1/tenants/{tenantId}/consumers/{consumerId}/api-keys/{k.ApiKey.Id}"))
            .RequireAuthorization(Permissions.ApiKeysManage);

        consumers.MapGet("/{consumerId:guid}/quota", async (Guid tenantId, Guid consumerId, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new GetQuotaQuery(tenantId, consumerId), ct)).ToOk(q => q.ToResponse()))
            .RequireAuthorization(Permissions.QuotasRead);

        consumers.MapPut("/{consumerId:guid}/quota", async (Guid tenantId, Guid consumerId, UpsertQuotaRequest b, IDispatcher d, CancellationToken ct) =>
                (await d.SendAsync(new UpsertQuotaCommand(tenantId, consumerId, b.TokensPerMinute, b.TokensPerMonth, b.MaxConcurrentRequests), ct)).ToOk(q => q.ToResponse()))
            .RequireAuthorization(Permissions.QuotasWrite);

        app.MapPost(Routes.Tenant + "/api-keys/{apiKeyId:guid}/revoke", async (Guid tenantId, Guid apiKeyId, IDispatcher d, TimeProvider clock, CancellationToken ct) =>
                (await d.SendAsync(new RevokeApiKeyCommand(tenantId, apiKeyId), ct)).ToOk(k => k.ToResponse(clock.GetUtcNow())))
            .WithTags("Access")
            .RequireAuthorization(Permissions.ApiKeysManage);

        return app;
    }
}
