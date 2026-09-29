using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Usage;
using SelfHostLlm.Gateway.Auth;
using SelfHostLlm.Gateway.Configuration;
using SelfHostLlm.Gateway.Endpoints;
using SelfHostLlm.Gateway.Streaming;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace SelfHostLlm.Gateway.Routing;

/// <summary>
/// Middleware trong pipeline YARP — phần tự xây của data plane:
/// quota → <see cref="RouteResolver"/> (virtual model/task → danh sách deployment) →
/// <see cref="FallbackExecutor"/> (mỗi lần thử = đổi cluster rồi gọi YARP forward) → metering.
/// Fallback chỉ xảy ra khi chưa có byte nào tới client: lỗi kết nối (IForwarderErrorFeature)
/// hoặc status transient bị transform chặn body.
/// </summary>
internal sealed partial class GatewayRoutingMiddleware(
    RequestDelegate next,
    GatewayStateStore store,
    IProxyStateLookup proxyState,
    FallbackExecutor fallback,
    IQuotaService quota,
    ITokenCounter tokenCounter,
    IUsageSink usageSink,
    TimeProvider timeProvider,
    ILogger<GatewayRoutingMiddleware> logger)
{
    private const int MaxRequestBodyBytes = 16 * 1024 * 1024;

    public async Task InvokeAsync(HttpContext http)
    {
        var state = store.Current;
        if (state is null)
        {
            await OpenAiErrors.WriteAsync(http, StatusCodes.Status503ServiceUnavailable, "Gateway chưa có cấu hình từ control plane.", "service_unavailable", "gateway.not_configured");
            return;
        }

        var caller = ApiCaller.From(http.User);
        var capability = http.Request.Path.StartsWithSegments("/v1/embeddings") ? ModelCapability.Embedding : ModelCapability.Chat;

        http.Request.EnableBuffering(bufferThreshold: 1024 * 1024, bufferLimit: MaxRequestBodyBytes);
        JsonObject? body;
        try
        {
            body = await JsonNode.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted) as JsonObject;
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null)
        {
            await OpenAiErrors.WriteAsync(http, StatusCodes.Status400BadRequest, "Body phải là JSON object.", "invalid_request_error", "invalid_json");
            return;
        }

        var requestedModel = (body["model"] as JsonValue)?.TryGetValue<string>(out var m) == true ? m : null;
        var task = (body["task"] as JsonValue)?.TryGetValue<string>(out var t) == true ? t : null;
        var stream = (body["stream"] as JsonValue)?.TryGetValue<bool>(out var s) == true && s;

        var lease = quota.TryAcquire(caller.ConsumerId, state.QuotaFor(caller.ConsumerId));
        if (lease.IsFailure)
        {
            http.Response.Headers.RetryAfter = RetryAfterSeconds(lease.Error!).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await OpenAiErrors.WriteAsync(http, lease.Error!);
            return;
        }

        using var _ = lease.Value;

        var plan = RouteResolver.Resolve(state.Routing, new RouteRequest(caller.TenantId, requestedModel, task, capability));
        if (plan.IsFailure)
        {
            await OpenAiErrors.WriteAsync(http, plan.Error!);
            return;
        }

        var started = timeProvider.GetTimestamp();
        var originalBody = http.Response.Body;
        await using var tap = new UsageTapStream(originalBody, isEventStream: stream);
        http.Response.Body = tap;

        FallbackResult<int> result;
        try
        {
            result = await fallback.ExecuteAsync(
                plan.Value.Candidates,
                (deployment, _) => ForwardAsync(http, state, deployment, body, stream),
                http.RequestAborted);
        }
        finally
        {
            http.Response.Body = originalBody;
        }

        tap.Complete();
        if (!result.IsSuccess)
        {
            await OpenAiErrors.WriteAsync(http, result.Error!);
        }

        Meter(http, caller, plan.Value, result, tap, body, started);
    }

    /// <summary>Một lần thử: gán request sang cluster của deployment rồi để YARP forward.</summary>
    private async Task<AttemptResult<int>> ForwardAsync(HttpContext http, GatewayState state, DeploymentEntry deployment, JsonObject body, bool stream)
    {
        if (!proxyState.TryGetCluster(SnapshotProxyConfigProvider.ClusterIdFor(deployment.Id), out var cluster))
        {
            return AttemptResult<int>.Transient(null, "cluster_not_ready");
        }

        http.Features.Set<IForwarderErrorFeature>(null);
        var attempt = new GatewayAttemptFeature(deployment.RemoteModelName, state.EngineKeyFor(deployment.Id), body, stream);
        http.Features.Set(attempt);

        // Body riêng cho deployment này (model → remote name, include_usage...). YARP forward
        // HttpContext.Request.Body, nên mỗi lần thử gán một stream mới.
        var upstreamBody = attempt.BuildUpstreamBody();
        var bodyStream = new MemoryStream(upstreamBody, writable: false);
        http.Response.RegisterForDispose(bodyStream);
        http.Request.Body = bodyStream;
        http.Request.ContentLength = upstreamBody.Length;
        http.ReassignProxyRequest(cluster);

        await next(http);

        if (http.Features.Get<IForwarderErrorFeature>() is { } error)
        {
            if (!http.Response.HasStarted)
            {
                http.Response.Clear();
                return AttemptResult<int>.Transient(null, error.Error.ToString());
            }

            // Lỗi giữa chừng khi đã stream một phần: không thể fallback nữa.
            return AttemptResult<int>.Success(http.Response.StatusCode, http.Response.StatusCode);
        }

        if (attempt.TransientStatus is { } transientStatus)
        {
            http.Response.Clear();
            return AttemptResult<int>.Transient(transientStatus, $"HTTP {transientStatus}");
        }

        return AttemptResult<int>.FromStatus(http.Response.StatusCode, http.Response.StatusCode);
    }

    /// <summary>
    /// Usage thật từ engine nếu có; không có thì đếm bằng tokenizer và đánh dấu ước lượng (QĐ-5).
    /// Ghi qua channel — không chặn response, lỗi ghi chỉ log.
    /// </summary>
    private void Meter(HttpContext http, ApiCaller caller, RoutePlan plan, FallbackResult<int> result, UsageTapStream tap, JsonObject body, long started)
    {
        var succeeded = result.IsSuccess && !result.IsClientError;
        var tokens = tap.Usage is { } usage
            ? TokenCount.Create(usage.PromptTokens, usage.CompletionTokens, isEstimated: false)
            : succeeded
                ? TokenCount.Create(tokenCounter.Count(PromptText(body)), tokenCounter.Count(tap.CompletionText), isEstimated: true)
                : Result<TokenCount>.Success(TokenCount.Zero);
        var tokenCount = tokens.IsSuccess ? tokens.Value : TokenCount.Zero;

        quota.RecordUsage(caller.ConsumerId, tokenCount.Total);

        var latencyMs = (int)Math.Min(int.MaxValue, timeProvider.GetElapsedTime(started).TotalMilliseconds);
        var record = UsageRecord.Create(
            caller.TenantId, caller.ApiKeyId, result.ServedBy?.Id, plan.RequestedModel ?? "", plan.Task, tokenCount,
            latencyMs, http.Response.StatusCode, result.UsedFallback, timeProvider.GetUtcNow());

        if (record.IsSuccess && !usageSink.TryWrite(record.Value))
        {
            LogUsageDropped(logger, caller.KeyPrefix);
        }

        LogServed(logger, result.ServedBy?.Id, http.Response.StatusCode, tokenCount.Prompt, tokenCount.Completion, tokenCount.IsEstimated,
            result.UsedFallback, latencyMs, caller.KeyPrefix);
    }

    /// <summary>Text của prompt để ước lượng token — chỉ dùng để đếm, không log.</summary>
    private static string PromptText(JsonObject body)
    {
        var text = new StringBuilder();
        if (body["messages"] is JsonArray messages)
        {
            foreach (var content in messages.Select(message => message?["content"]))
            {
                AppendText(text, content);
            }
        }

        AppendText(text, body["input"]);
        return text.ToString();
    }

    private static void AppendText(StringBuilder text, JsonNode? node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var s):
                text.Append(s).Append('\n');
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    AppendText(text, item is JsonObject part ? part["text"] : item);
                }

                break;
        }
    }

    private int RetryAfterSeconds(Error error)
    {
        if (error.Code != "quota.budget_exceeded")
        {
            return error.Code == "quota.concurrency_exceeded" ? 1 : 60;
        }

        var now = timeProvider.GetUtcNow();
        var nextMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        return (int)Math.Ceiling((nextMonth - now).TotalSeconds);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Served deployment={DeploymentId} status={StatusCode} prompt={PromptTokens} completion={CompletionTokens} estimated={Estimated} fallback={UsedFallback} latency={LatencyMs}ms key={KeyPrefix}")]
    private static partial void LogServed(ILogger logger, Guid? deploymentId, int statusCode, int promptTokens, int completionTokens,
        bool estimated, bool usedFallback, int latencyMs, string keyPrefix);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hàng đợi usage đầy — bỏ một usage record (key {KeyPrefix})")]
    private static partial void LogUsageDropped(ILogger logger, string keyPrefix);
}
