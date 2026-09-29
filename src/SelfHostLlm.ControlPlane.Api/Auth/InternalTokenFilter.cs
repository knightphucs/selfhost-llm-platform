using System.Security.Cryptography;
using System.Text;
using SelfHostLlm.Contracts.Internal;

namespace SelfHostLlm.ControlPlane.Api.Auth;

/// <summary>
/// Bảo vệ endpoint nội bộ (Gateway → CP) bằng token dùng chung <c>InternalApi:Token</c>.
/// So sánh SHA-256 của hai chuỗi bằng <see cref="CryptographicOperations.FixedTimeEquals"/> để
/// không lộ độ dài hay vị trí khác nhau qua thời gian phản hồi.
/// </summary>
internal sealed partial class InternalTokenFilter(IConfiguration configuration, ILogger<InternalTokenFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var expected = configuration["InternalApi:Token"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            LogNotConfigured(logger);
            return TypedResults.Problem("Endpoint nội bộ chưa được cấu hình.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var provided = context.HttpContext.Request.Headers[ConfigSnapshotDto.InternalTokenHeader].ToString();
        return Matches(provided, expected)
            ? await next(context)
            : TypedResults.Problem("Thiếu hoặc sai internal token.", statusCode: StatusCodes.Status401Unauthorized);
    }

    private static bool Matches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    [LoggerMessage(Level = LogLevel.Warning, Message = "InternalApi:Token chưa cấu hình — từ chối /internal/config-snapshot")]
    private static partial void LogNotConfigured(ILogger logger);
}
