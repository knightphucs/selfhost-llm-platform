using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SelfHostLlm.ControlPlane.Api.Hosting;

/// <summary>
/// Bắt exception không lường trước và trả RFC 7807 ProblemDetails.
/// Lỗi nghiệp vụ dự đoán được đi qua <c>Result&lt;T&gt;</c>, không tới đây.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Đã xảy ra lỗi không mong muốn.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception chưa xử lý tại {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, PathString path);
}
