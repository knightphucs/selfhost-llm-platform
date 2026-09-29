using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SelfHostLlm.Contracts.Internal;

namespace SelfHostLlm.Gateway.Configuration;

internal abstract record FetchResult
{
    public sealed record Updated(ConfigSnapshotDto Snapshot, string ETag) : FetchResult;

    public sealed record NotModified : FetchResult;

    public sealed record Failed(string Reason) : FetchResult;
}

internal interface IConfigSnapshotFetcher
{
    Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken);
}

/// <summary>Kéo snapshot từ control plane: <c>GET /internal/config-snapshot</c> kèm token nội bộ và ETag.</summary>
internal sealed class HttpConfigSnapshotFetcher(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IConfigSnapshotFetcher
{
    public const string HttpClientName = "control-plane";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["ControlPlane:BaseUrl"];
        var token = configuration["ControlPlane:InternalToken"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
        {
            return new FetchResult.Failed("Thiếu cấu hình ControlPlane:BaseUrl / ControlPlane:InternalToken.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), "/internal/config-snapshot"));
        request.Headers.Add(ConfigSnapshotDto.InternalTokenHeader, token);
        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new FetchResult.NotModified();
            }

            if (!response.IsSuccessStatusCode)
            {
                return new FetchResult.Failed($"Control plane trả HTTP {(int)response.StatusCode}.");
            }

            var snapshot = await response.Content.ReadFromJsonAsync<ConfigSnapshotDto>(JsonOptions, cancellationToken);
            return snapshot is null
                ? new FetchResult.Failed("Snapshot rỗng.")
                : new FetchResult.Updated(snapshot, response.Headers.ETag?.ToString() ?? "");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new FetchResult.Failed(ex.GetType().Name);
        }
    }
}
