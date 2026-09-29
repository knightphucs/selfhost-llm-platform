using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.ControlPlane.Api.Http;

/// <summary>
/// Đổi <see cref="Result"/> sang HTTP. Lỗi theo RFC 7807 ProblemDetails, kèm extension <c>code</c>
/// (mã lỗi ổn định cho client); lỗi validation có <c>errors</c> theo field.
/// </summary>
internal static class ResultHttpExtensions
{
    public static IResult ToOk<T, TResponse>(this Result<T> result, Func<T, TResponse> map) =>
        result.IsSuccess ? TypedResults.Ok(map(result.Value)) : result.Error!.ToProblem();

    public static IResult ToCreated<T, TResponse>(this Result<T> result, Func<T, TResponse> map, Func<T, string> location) =>
        result.IsSuccess ? TypedResults.Created(location(result.Value), map(result.Value)) : result.Error!.ToProblem();

    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Kind == ErrorKind.Validation && error.Details is { Count: > 0 } details)
        {
            return TypedResults.ValidationProblem(
                details.ToDictionary(kv => kv.Key, kv => kv.Value),
                detail: error.Message,
                extensions: extensions);
        }

        return TypedResults.Problem(detail: error.Message, statusCode: StatusCodeOf(error.Kind), extensions: extensions);
    }

    public static int StatusCodeOf(ErrorKind kind) => kind switch
    {
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError,
    };
}
