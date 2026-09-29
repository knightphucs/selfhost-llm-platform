namespace SelfHostLlm.Domain.Common;

/// <summary>Lỗi nghiệp vụ dự đoán được. <paramref name="Code"/> ổn định, dùng cho client và test.</summary>
/// <param name="Details">Lỗi theo từng field (validation); host đổ vào <c>errors</c> của ProblemDetails.</param>
public sealed record Error(
    ErrorKind Kind,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> details) =>
        new(ErrorKind.Validation, code, message, details);

    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);

    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);

    public static Error Unavailable(string code, string message) => new(ErrorKind.Unavailable, code, message);

    public static Error RateLimited(string code, string message) => new(ErrorKind.RateLimited, code, message);
}
