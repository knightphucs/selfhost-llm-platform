using SelfHostLlm.Domain.Providers;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Adapter tới một loại backend suy luận. Proxy request chat/embeddings do Gateway (YARP) lo —
/// port này phục vụ các lời gọi quản trị: probe health, liệt kê model phía engine.
/// </summary>
public interface IInferenceProvider
{
    /// <summary>Probe nhanh (<c>GET /v1/models</c>). Không ném exception cho lỗi mạng — trả kết quả unhealthy.</summary>
    Task<ProbeResult> ProbeAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListModelsAsync(InferenceEndpoint endpoint, CancellationToken cancellationToken);
}

public interface IInferenceProviderFactory
{
    IInferenceProvider For(ProviderKind kind);
}

/// <param name="ApiKey">API key phía engine (plaintext, chỉ trong bộ nhớ). <see cref="ToString"/> che nó.</param>
public sealed record InferenceEndpoint(Uri BaseUrl, string? ApiKey)
{
    public override string ToString() => $"{nameof(InferenceEndpoint)} {{ BaseUrl = {BaseUrl}, ApiKey = {(ApiKey is null ? "null" : "***")} }}";
}

public sealed record ProbeResult(bool Healthy, int LatencyMs, int? StatusCode, string? Error);

/// <summary>
/// Đếm token dự phòng khi engine không trả <c>usage</c>. Kết quả là ƯỚC LƯỢNG — bản ghi usage
/// phải đánh dấu <c>tokens_estimated = true</c> (QĐ-5).
/// </summary>
public interface ITokenCounter
{
    int Count(string text);
}
