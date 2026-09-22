namespace SelfHostLlm.Domain.Common;

/// <summary>
/// Cho phép code generic (pipeline behavior) tạo kết quả thất bại đúng kiểu
/// <typeparamref name="TSelf"/> — <see cref="Result"/> hoặc <see cref="Result{T}"/> — mà không cần reflection.
/// </summary>
public interface IFailureFactory<out TSelf>
{
    static abstract TSelf FromError(Error error);
}
