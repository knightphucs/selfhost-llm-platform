namespace SelfHostLlm.Domain.Common;

/// <summary>Lỗi nghiệp vụ dự đoán được. <paramref name="Code"/> ổn định, dùng cho client và test.</summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);

    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);

    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);

    public static Error Unavailable(string code, string message) => new(ErrorKind.Unavailable, code, message);
}
