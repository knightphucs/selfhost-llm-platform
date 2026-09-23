using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Gateway.Auth;
using SelfHostLlm.Gateway.Configuration;
using SelfHostLlm.Gateway.Endpoints;
using SelfHostLlm.Gateway.Metering;
using SelfHostLlm.Gateway.Routing;
using Yarp.ReverseProxy.Configuration;

namespace SelfHostLlm.Gateway;

internal static class GatewaySetup
{
    /// <summary>Đăng ký phần tự xây của Gateway. Gọi TRƯỚC AddApplication để baseline quota lấy từ snapshot.</summary>
    public static IServiceCollection AddGateway(this IServiceCollection services)
    {
        services.AddSingleton<GatewayStateStore>();
        services.AddSingleton<IMonthlyUsageBaseline>(sp => sp.GetRequiredService<GatewayStateStore>());

        services.AddHttpClient(HttpConfigSnapshotFetcher.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IConfigSnapshotFetcher, HttpConfigSnapshotFetcher>();
        services.AddSingleton<SnapshotRefresher>();
        services.AddHostedService<ConfigSnapshotPoller>();

        services.AddSingleton<ChannelUsageSink>();
        services.AddSingleton<IUsageSink>(sp => sp.GetRequiredService<ChannelUsageSink>());
        services.AddHostedService<UsageFlushService>();

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(ApiKeyAuthenticationHandler.SchemeName).RequireAuthenticatedUser().Build());

        services.AddSingleton<IProxyConfigProvider, SnapshotProxyConfigProvider>();
        services.AddReverseProxy().AddTransforms(GatewayTransforms.Apply);

        services.AddExceptionHandler<OpenAiExceptionHandler>();
        services.AddProblemDetails();

        services.AddHealthChecks().AddCheck<SnapshotHealthCheck>("config-snapshot", tags: ["ready"]);
        return services;
    }

    public static IEndpointRouteBuilder MapGateway(this IEndpointRouteBuilder app)
    {
        app.MapModelsEndpoint();
        app.MapReverseProxy(pipeline =>
        {
            pipeline.UseMiddleware<GatewayRoutingMiddleware>();
            pipeline.UseLoadBalancing();
        });
        return app;
    }
}

/// <summary>Gateway chỉ sẵn sàng khi đã có config snapshot (từ CP hoặc file cache).</summary>
internal sealed class SnapshotHealthCheck(GatewayStateStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Current is null
            ? HealthCheckResult.Unhealthy("Chưa có config snapshot.")
            : HealthCheckResult.Healthy($"ETag {store.Current.ETag}"));
}
