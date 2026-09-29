using Microsoft.Net.Http.Headers;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Configuration;
using SelfHostLlm.ControlPlane.Api.Auth;
using SelfHostLlm.ControlPlane.Api.Http;
using SelfHostLlm.ControlPlane.Api.Mapping;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

/// <summary>
/// Hợp đồng Control plane → Gateway. Gateway kéo (pull) định kỳ kèm ETag; CP không nằm trên
/// đường đi của request suy luận (bất biến 1).
/// </summary>
internal static class InternalEndpoints
{
    public static IEndpointRouteBuilder MapInternalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/config-snapshot", async (HttpContext http, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new GetConfigSnapshotQuery(), ct);
                if (result.IsFailure)
                {
                    return result.Error!.ToProblem();
                }

                var snapshot = result.Value.ToDto();
                var etag = SnapshotMapper.ComputeETag(snapshot);
                http.Response.Headers[HeaderNames.ETag] = etag;
                http.Response.Headers[HeaderNames.CacheControl] = "no-store";

                return http.Request.Headers[HeaderNames.IfNoneMatch].ToString() == etag
                    ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
                    : TypedResults.Ok(snapshot);
            })
            .AllowAnonymous()
            .AddEndpointFilter<InternalTokenFilter>()
            .ExcludeFromDescription();

        return app;
    }
}
