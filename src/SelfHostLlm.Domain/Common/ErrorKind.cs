namespace SelfHostLlm.Domain.Common;

/// <summary>Phân loại lỗi nghiệp vụ; host project map sang HTTP status.</summary>
public enum ErrorKind
{
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Forbidden,
    Unavailable,
}
