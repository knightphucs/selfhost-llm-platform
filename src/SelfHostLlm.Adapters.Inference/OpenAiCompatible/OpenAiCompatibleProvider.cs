using System.Net.Http.Headers;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Contracts.OpenAi;

namespace SelfHostLlm.Adapters.Inference.OpenAiCompatible;

/// <summary>
/// Adapter gốc cho mọi engine nói chuẩn OpenAI (<c>/v1</c>): Ollama, vLLM, TGI, llama.cpp, MLX.
/// HttpClient đã có timeout + circuit breaker (không retry — retry là việc của FallbackExecutor).
/// </summary>
internal sealed class OpenAiCompatibleProvider(IHttpClientFactory httpClientFactory, TimeProvider timeProvider) : IInferenceProvider
{
    public const string HttpClientName = "inference";

    public async Task<ProbeResult> ProbeAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var started = timeProvider.GetTimestamp();
        try
        {
            using var response = await SendModelsRequestAsync(endpoint, cancellationToken);
            var latency = ElapsedMs(started);
            return response.IsSuccessStatusCode
                ? new ProbeResult(true, latency, (int)response.StatusCode, null)
                : new ProbeResult(false, latency, (int)response.StatusCode, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (IsNetworkFailure(ex, cancellationToken))
        {
            return new ProbeResult(false, ElapsedMs(started), null, ex.GetType().Name);
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        try
        {
            using var response = await SendModelsRequestAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            var models = await response.Content.ReadFromJsonAsync<ModelListResponse>(OpenAiJson.Options, cancellationToken);
            return models?.Data.Select(m => m.Id).ToList() ?? [];
        }
        catch (BrokenCircuitException ex)
        {
            throw new UpstreamUnavailableException("Circuit breaker tới engine đang mở.", ex);
        }
    }

    private async Task<HttpResponseMessage> SendModelsRequestAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint.BaseUrl, "/v1/models"));
        if (!string.IsNullOrEmpty(endpoint.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
        }

        return await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
    }

    private int ElapsedMs(long started) => (int)Math.Min(int.MaxValue, timeProvider.GetElapsedTime(started).TotalMilliseconds);

    /// <summary>Lỗi mạng/timeout/circuit mở → probe unhealthy. Người gọi huỷ thì để exception đi lên.</summary>
    private static bool IsNetworkFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException or IOException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}

internal sealed class InferenceProviderFactory(OpenAiCompatibleProvider openAiCompatible) : IInferenceProviderFactory
{
    /// <summary>Mọi engine hiện dùng adapter OpenAI-compatible; loại khác chỉ thêm khi thật sự khác chuẩn.</summary>
    public IInferenceProvider For(Domain.Providers.ProviderKind kind) => openAiCompatible;
}
