using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SelfHostLlm.Adapters.Inference.OpenAiCompatible;
using SelfHostLlm.Adapters.Inference.Tokenization;
using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Adapters.Inference;

public static class DependencyInjection
{
    /// <summary>Timeout cho lời gọi quản trị (probe, list model) — không áp cho proxy chat của Gateway.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Adapter inference + HttpClient có timeout và circuit breaker (Polly v8). KHÔNG retry ở tầng
    /// HTTP: fallback sang deployment khác là việc của RouteResolver + FallbackExecutor, tránh retry
    /// hai tầng nhân nhau. Circuit breaker tách theo authority (host:port) — một engine chết không
    /// chặn probe tới engine khác.
    /// </summary>
    public static IServiceCollection AddInferenceAdapters(this IServiceCollection services)
    {
        services.TryAddTimeProvider();

        services.AddHttpClient(OpenAiCompatibleProvider.HttpClientName)
            .AddResilienceHandler("inference-admin", pipeline =>
            {
                pipeline.AddTimeout(ProbeTimeout);
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    FailureRatio = 0.8,
                    BreakDuration = TimeSpan.FromSeconds(15),
                });
            })
            .SelectPipelineByAuthority();

        services.AddSingleton<OpenAiCompatibleProvider>();
        services.AddSingleton<IInferenceProviderFactory, InferenceProviderFactory>();
        services.AddSingleton<ITokenCounter, TiktokenTokenCounter>();
        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
