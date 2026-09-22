using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Lưu mọi thay đổi (kể cả audit log) trong một transaction. Vi phạm ràng buộc DB được đổi
    /// thành lỗi nghiệp vụ: unique → <see cref="ErrorKind.Conflict"/>, FK (kể cả composite FK chặn
    /// tham chiếu chéo tenant) → <see cref="ErrorKind.Validation"/>.
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
