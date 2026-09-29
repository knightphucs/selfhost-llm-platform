using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Common;

internal static class UseCaseExtensions
{
    /// <summary>Lưu unit of work; thành công thì trả <paramref name="value"/>, lỗi DB thì trả lỗi đã được map.</summary>
    public static async Task<Result<T>> SaveAndReturnAsync<T>(this IUnitOfWork unitOfWork, T value, CancellationToken cancellationToken)
    {
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result<T>.Success(value) : Result<T>.Failure(saved.Error!);
    }

    public static Error NotFound<TEntity>(Guid id) =>
        Error.NotFound($"{typeof(TEntity).Name.ToLowerInvariant()}.not_found", $"Không tìm thấy {typeof(TEntity).Name} {id}.");
}
