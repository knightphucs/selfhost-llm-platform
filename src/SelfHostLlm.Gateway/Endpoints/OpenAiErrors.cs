using SelfHostLlm.Contracts.OpenAi;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Gateway.Endpoints;

/// <summary>Gateway trả lỗi theo format OpenAI (<c>{ "error": { message, type, code } }</c>) để SDK client hiểu được.</summary>
internal static class OpenAiErrors
{
    public static Task WriteAsync(HttpContext http, int statusCode, string message, string type, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        http.Response.StatusCode = statusCode;
        return http.Response.WriteAsJsonAsync(OpenAiErrorResponse.Create(message, type, code), OpenAiJson.Options);
    }

    public static Task WriteAsync(HttpContext http, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (status, type) = error.Kind switch
        {
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "invalid_request_error"),
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "invalid_request_error"),
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "authentication_error"),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "permission_error"),
            ErrorKind.RateLimited => (StatusCodes.Status429TooManyRequests, "rate_limit_error"),
            ErrorKind.Unavailable => (StatusCodes.Status503ServiceUnavailable, "service_unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "server_error"),
        };
        return WriteAsync(http, status, error.Message, type, error.Code);
    }
}

/// <summary>Exception không lường trước → 500 theo format OpenAI (không lộ chi tiết nội bộ).</summary>
internal sealed partial class OpenAiExceptionHandler(ILogger<OpenAiExceptionHandler> logger) : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception, httpContext.Request.Path);
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        await OpenAiErrors.WriteAsync(httpContext, StatusCodes.Status500InternalServerError, "Lỗi nội bộ của gateway.", "server_error");
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception chưa xử lý tại {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, PathString path);
}
