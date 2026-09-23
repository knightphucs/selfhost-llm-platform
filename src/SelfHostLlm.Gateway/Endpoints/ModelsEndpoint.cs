using System.Security.Claims;
using SelfHostLlm.Contracts.OpenAi;
using SelfHostLlm.Gateway.Auth;
using SelfHostLlm.Gateway.Configuration;

namespace SelfHostLlm.Gateway.Endpoints;

internal static class ModelsEndpoint
{
    /// <summary><c>GET /v1/models</c>: virtual model và model của tenant người gọi — không lộ của tenant khác.</summary>
    public static IEndpointRouteBuilder MapModelsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/models", (ClaimsPrincipal user, GatewayStateStore store) =>
        {
            var tenantId = ApiCaller.From(user).TenantId;
            var state = store.Current;
            var data = (state?.VirtualModels ?? [])
                .Where(v => v.TenantId == tenantId)
                .Select(v => new ModelInfo { Id = v.Name, OwnedBy = "selfhost-llm/virtual" })
                .Concat((state?.Models ?? [])
                    .Where(m => m.TenantId == tenantId)
                    .Select(m => new ModelInfo { Id = m.Name, OwnedBy = "selfhost-llm/model" }))
                .OrderBy(i => i.Id, StringComparer.Ordinal)
                .ToList();
            return TypedResults.Json(new ModelListResponse { Data = data }, OpenAiJson.Options);
        });

        return app;
    }
}
