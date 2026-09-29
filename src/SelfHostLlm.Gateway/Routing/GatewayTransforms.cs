using System.Net.Http.Headers;
using SelfHostLlm.Application.Routing;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SelfHostLlm.Gateway.Routing;

/// <summary>Transform YARP cho mọi route của gateway.</summary>
internal static class GatewayTransforms
{
    public static void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transform =>
        {
            // API key của client (sk-…) KHÔNG BAO GIỜ được forward tới engine.
            transform.ProxyRequest.Headers.Authorization = null;

            var attempt = transform.HttpContext.Features.Get<GatewayAttemptFeature>();
            if (attempt is null)
            {
                return ValueTask.CompletedTask;
            }

            // Body đã được middleware viết lại trên HttpContext.Request (YARP 2.x không cho thay
            // HttpContent của request gửi đi) — ở đây chỉ đổi credential.
            if (attempt.EngineKey is not null)
            {
                transform.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attempt.EngineKey);
            }

            return ValueTask.CompletedTask;
        });

        context.AddResponseTransform(transform =>
        {
            var attempt = transform.HttpContext.Features.Get<GatewayAttemptFeature>();
            if (attempt is null || transform.ProxyResponse is null)
            {
                return ValueTask.CompletedTask;
            }

            // Engine lỗi tạm thời: chặn body để chưa có byte nào tới client → middleware còn
            // fallback được sang deployment kế tiếp (fallback chỉ khả thi trước byte đầu tiên).
            var status = (int)transform.ProxyResponse.StatusCode;
            if (UpstreamStatus.Classify(status) == AttemptOutcome.Transient)
            {
                transform.SuppressResponseBody = true;
                attempt.MarkTransient(status);
            }

            return ValueTask.CompletedTask;
        });
    }
}
