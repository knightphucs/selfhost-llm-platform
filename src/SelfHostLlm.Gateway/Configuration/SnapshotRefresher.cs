using System.Text.Json;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Contracts.Internal;

namespace SelfHostLlm.Gateway.Configuration;

/// <summary>
/// Một lượt làm mới cấu hình. CP lỗi → GIỮ state cũ (bất biến 1). Mỗi snapshot mới được ghi xuống
/// file cache để Gateway khởi động lại vẫn phục vụ được khi CP chưa lên. File cache chỉ chứa hash
/// API key và ciphertext key engine — không có plaintext.
/// </summary>
internal sealed partial class SnapshotRefresher(
    IConfigSnapshotFetcher fetcher,
    GatewayStateStore store,
    ISecretProtector secrets,
    IConfiguration configuration,
    ILogger<SnapshotRefresher> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string CachePath => configuration["Gateway:SnapshotCachePath"]
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SelfHostLlm", "gateway-snapshot.json");

    /// <summary>Trả true nếu sau lượt này Gateway có cấu hình để phục vụ.</summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await fetcher.FetchAsync(store.Current?.ETag, cancellationToken);
        switch (result)
        {
            case FetchResult.Updated updated:
                Apply(updated.Snapshot, updated.ETag);
                await SaveCacheAsync(updated, cancellationToken);
                return true;

            case FetchResult.NotModified:
                return store.Current is not null;

            case FetchResult.Failed failed:
                LogFetchFailed(logger, failed.Reason, store.Current is not null);
                return store.Current is not null || await LoadCacheAsync(cancellationToken);

            default:
                return store.Current is not null;
        }
    }

    private void Apply(ConfigSnapshotDto snapshot, string etag)
    {
        var warnings = new List<string>();
        store.Replace(GatewayState.From(snapshot, etag, secrets, warnings));
        foreach (var warning in warnings)
        {
            LogSnapshotWarning(logger, warning);
        }

        LogApplied(logger, snapshot.Deployments.Count, snapshot.ApiKeys.Count, etag);
    }

    private async Task SaveCacheAsync(FetchResult.Updated updated, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var temp = CachePath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new CachedSnapshot(updated.ETag, updated.Snapshot), JsonOptions), cancellationToken);
            File.Move(temp, CachePath, overwrite: true);
        }
        catch (IOException ex)
        {
            LogCacheWriteFailed(logger, ex);
        }
    }

    private async Task<bool> LoadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(CachePath))
        {
            return false;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<CachedSnapshot>(await File.ReadAllTextAsync(CachePath, cancellationToken), JsonOptions);
            if (cached is null)
            {
                return false;
            }

            Apply(cached.Snapshot, cached.ETag);
            LogLoadedFromCache(logger, CachePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            LogCacheWriteFailed(logger, ex);
            return false;
        }
    }

    private sealed record CachedSnapshot(string ETag, ConfigSnapshotDto Snapshot);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không lấy được config snapshot ({Reason}); đang giữ snapshot cũ: {HasState}")]
    private static partial void LogFetchFailed(ILogger logger, string reason, bool hasState);

    [LoggerMessage(Level = LogLevel.Information, Message = "Áp dụng config snapshot: {Deployments} deployment, {ApiKeys} api key (ETag {ETag})")]
    private static partial void LogApplied(ILogger logger, int deployments, int apiKeys, string etag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Snapshot: {Warning}")]
    private static partial void LogSnapshotWarning(ILogger logger, string warning);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nạp config snapshot từ file cache {Path} (control plane không liên lạc được)")]
    private static partial void LogLoadedFromCache(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lỗi đọc/ghi file cache snapshot")]
    private static partial void LogCacheWriteFailed(ILogger logger, Exception exception);
}

/// <summary>Làm mới cấu hình định kỳ (mặc định 30 giây).</summary>
internal sealed class ConfigSnapshotPoller(SnapshotRefresher refresher, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("Gateway:SnapshotRefreshSeconds", 30));
        using var timer = new PeriodicTimer(interval);
        do
        {
            await refresher.RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
