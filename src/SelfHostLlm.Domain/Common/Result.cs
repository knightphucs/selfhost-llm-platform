namespace SelfHostLlm.Domain.Common;

/// <summary>
/// Kết quả của một thao tác nghiệp vụ không trả giá trị.
/// Dùng thay cho exception với các lỗi dự đoán được.
/// </summary>
public class Result
{
    private protected Result(Error? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Kết quả của một thao tác nghiệp vụ trả về <typeparamref name="T"/>.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>Giá trị khi thành công. Đọc khi thất bại là lỗi lập trình nên ném exception.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Không đọc được Value của Result thất bại ({Error!.Code}).");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

#pragma warning disable CA2225 // Đã có Success/Failure làm phương thức thay thế có tên.
    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
#pragma warning restore CA2225
}
