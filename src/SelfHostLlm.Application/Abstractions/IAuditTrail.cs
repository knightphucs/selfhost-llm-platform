using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Ghi vết thao tác quản trị. Bản ghi được thêm vào cùng unit of work với thay đổi nên được lưu
/// (hoặc không được lưu) cùng lúc. <paramref name="before"/>/<paramref name="after"/> phải là
/// snapshot không chứa secret.
/// </summary>
public interface IAuditTrail
{
    void Record(Guid tenantId, AuditAction action, string entityType, string entityId, object? before, object? after);
}

/// <summary>Port ghi audit log xuống lưu trữ — chỉ có thêm, không có sửa/xoá.</summary>
public interface IAuditLogWriter
{
    void Add(AuditLog auditLog);
}
