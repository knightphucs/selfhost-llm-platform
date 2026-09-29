using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Gateway.Configuration;
using SelfHostLlm.Gateway.Endpoints;

namespace SelfHostLlm.Gateway.Auth;

/// <summary>
/// Xác thực <c>Authorization: Bearer sk-…</c>: kiểm định dạng → SHA-256 → tra bảng hash trong
/// snapshot → kiểm hạn. Không truy vấn DB (bất biến 2). Challenge trả 401 theo format OpenAI.
/// </summary>
internal sealed partial class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    GatewayStateStore store,
    ApiKeyHasher hasher,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string ConsumerIdClaim = "consumer_id";
    public const string ApiKeyIdClaim = "api_key_id";
    public const string KeyPrefixClaim = "key_prefix";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var key = header["Bearer ".Length..].Trim();
        if (!ApiKeyHasher.IsWellFormed(key))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key sai định dạng."));
        }

        var entry = store.Current?.FindApiKey(hasher.ComputeHash(key));
        if (entry is null || (entry.ExpiresAt is { } expiresAt && expiresAt <= timeProvider.GetUtcNow()))
        {
            // Chỉ log prefix — không bao giờ log key.
            LogRejected(Logger, key[..ApiKeyHasher.DisplayPrefixLength]);
            return Task.FromResult(AuthenticateResult.Fail("API key không hợp lệ, đã thu hồi hoặc hết hạn."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, entry.Id.ToString()),
                new Claim(PlatformClaims.TenantId, entry.TenantId.ToString()),
                new Claim(ConsumerIdClaim, entry.ConsumerId.ToString()),
                new Claim(ApiKeyIdClaim, entry.Id.ToString()),
                new Claim(KeyPrefixClaim, entry.KeyPrefix),
            ],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        OpenAiErrors.WriteAsync(Context, StatusCodes.Status401Unauthorized,
            "Thiếu hoặc sai API key. Gửi header 'Authorization: Bearer sk-...'.", "authentication_error", "invalid_api_key");

    [LoggerMessage(Level = LogLevel.Information, Message = "Từ chối API key có prefix {KeyPrefix}")]
    private static partial void LogRejected(ILogger logger, string keyPrefix);
}

/// <summary>Danh tính người gọi đã xác thực, đọc từ claims.</summary>
internal sealed record ApiCaller(Guid TenantId, Guid ConsumerId, Guid ApiKeyId, string KeyPrefix)
{
    public static ApiCaller From(ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(PlatformClaims.TenantId)!),
        Guid.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.ConsumerIdClaim)!),
        Guid.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.ApiKeyIdClaim)!),
        user.FindFirstValue(ApiKeyAuthenticationHandler.KeyPrefixClaim)!);
}
