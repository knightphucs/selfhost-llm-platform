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

    /// <summary>Vượt quota (rate, budget hoặc concurrency) → 429.</summary>
    RateLimited,
}
