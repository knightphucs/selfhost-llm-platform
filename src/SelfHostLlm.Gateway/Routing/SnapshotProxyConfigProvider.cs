using Microsoft.Extensions.Primitives;
using SelfHostLlm.Gateway.Configuration;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace SelfHostLlm.Gateway.Routing;

/// <summary>
/// Cấu hình YARP sinh từ config snapshot (QĐ-3): mỗi deployment một cluster <c>dep-{id}</c> với
/// một destination = base URL. Hai route (<c>/v1/chat/completions</c>, <c>/v1/embeddings</c>) trỏ
/// cluster giữ chỗ — <see cref="GatewayRoutingMiddleware"/> chọn cluster thật cho từng lần thử vì
/// YARP không match theo field <c>model</c> trong body.
/// </summary>
internal sealed class SnapshotProxyConfigProvider(GatewayStateStore store, IConfiguration configuration) : IProxyConfigProvider
{
    public const string PlaceholderClusterId = "unrouted";
    public const string ChatRouteId = "chat-completions";
    public const string EmbeddingsRouteId = "embeddings";

    public static string ClusterIdFor(Guid deploymentId) => $"dep-{deploymentId:N}";

    public IProxyConfig GetConfig()
    {
        var activityTimeout = TimeSpan.FromSeconds(configuration.GetValue("Gateway:UpstreamActivityTimeoutSeconds", 120));
        var clusters = new List<ClusterConfig> { new() { ClusterId = PlaceholderClusterId } };

        foreach (var deployment in store.Current?.Deployments ?? [])
        {
            clusters.Add(new ClusterConfig
            {
                ClusterId = ClusterIdFor(deployment.Id),
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["engine"] = new() { Address = deployment.BaseUrl.ToString() },
                },

                // LLM sinh chậm: timeout theo khoảng lặng giữa hai lần đọc, không theo tổng thời gian.
                HttpRequest = new ForwarderRequestConfig { ActivityTimeout = activityTimeout },
            });
        }

        RouteConfig[] routes =
        [
            new() { RouteId = ChatRouteId, ClusterId = PlaceholderClusterId, Match = new RouteMatch { Path = "/v1/chat/completions", Methods = ["POST"] } },
            new() { RouteId = EmbeddingsRouteId, ClusterId = PlaceholderClusterId, Match = new RouteMatch { Path = "/v1/embeddings", Methods = ["POST"] } },
        ];

        return new SnapshotProxyConfig(routes, clusters, store.ChangeToken);
    }

    private sealed class SnapshotProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, IChangeToken changeToken)
        : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes => routes;

        public IReadOnlyList<ClusterConfig> Clusters => clusters;

        public IChangeToken ChangeToken => changeToken;
    }
}
