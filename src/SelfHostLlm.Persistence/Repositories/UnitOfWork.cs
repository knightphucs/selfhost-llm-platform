using Microsoft.EntityFrameworkCore;
using Npgsql;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Persistence.Repositories;

/// <summary>
/// Lưu thay đổi và đổi vi phạm ràng buộc DB thành lỗi nghiệp vụ, để race condition (hai request
/// cùng tạo một tên) hay tham chiếu chéo tenant bị composite FK chặn không thành lỗi 500.
/// </summary>
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && Map(ex, pg) is { } error)
        {
            // Bỏ thay đổi hỏng để lần SaveChanges sau trong cùng scope không ghi lại chúng (kể cả audit).
            db.ChangeTracker.Clear();
            return error;
        }
    }

    private static Error? Map(DbUpdateException exception, PostgresException pg) => pg.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => Error.Conflict(
            "persistence.duplicate",
            $"Dữ liệu đã tồn tại (vi phạm {pg.ConstraintName})."),

        // Xoá một bản ghi đang được tham chiếu (FK Restrict) → xung đột trạng thái.
        PostgresErrorCodes.ForeignKeyViolation when exception.Entries.Any(e => e.State == EntityState.Deleted) => Error.Conflict(
            "persistence.in_use",
            $"Không thể xoá: dữ liệu đang được tham chiếu ({pg.ConstraintName})."),

        // Thêm/sửa tham chiếu tới bản ghi không tồn tại hoặc thuộc tenant khác (composite FK).
        PostgresErrorCodes.ForeignKeyViolation => Error.Validation(
            "persistence.invalid_reference",
            $"Tham chiếu không hợp lệ: bản ghi không tồn tại hoặc thuộc tenant khác ({pg.ConstraintName})."),

        PostgresErrorCodes.CheckViolation => Error.Validation(
            "persistence.check_violation",
            $"Dữ liệu vi phạm ràng buộc {pg.ConstraintName}."),

        _ => null,
    };
}
