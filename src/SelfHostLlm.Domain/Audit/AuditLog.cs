using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Audit;

/// <summary>
/// Vết một thao tác quản trị: ai · làm gì · lên thực thể nào · khi nào.
/// Append-only — không có behavior sửa, và không có endpoint sửa/xoá.
/// Request suy luận thông thường KHÔNG ghi vào đây mà vào UsageRecord.
/// </summary>
public sealed class AuditLog : Entity<long>, ITenantScoped
{
    private AuditLog()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>Người thực hiện; null khi là thao tác của hệ thống (seed, job nền).</summary>
    public Guid? ActorUserId { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>Tên entity bị tác động, ví dụ <c>Deployment</c>.</summary>
    public string EntityType { get; private set; } = null!;

    public string EntityId { get; private set; } = null!;

    /// <summary>Snapshot JSON trước thao tác. Application chịu trách nhiệm loại bỏ secret trước khi serialize.</summary>
    public string? BeforeValue { get; private set; }

    public string? AfterValue { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static Result<AuditLog> Create(
        Guid tenantId,
        Guid? actorUserId,
        AuditAction action,
        string entityType,
        string entityId,
        string? beforeValue,
        string? afterValue,
        string? ipAddress,
        DateTimeOffset occurredAt)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "audit.tenant_id"),
            Enum.IsDefined(action) ? null : Error.Validation("audit.action.invalid", "AuditAction không hợp lệ."),
            Guard.NotBlank(entityType, "audit.entity_type", 100),
            Guard.NotBlank(entityId, "audit.entity_id", 100));
        if (error is not null)
        {
            return error;
        }

        return new AuditLog
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeValue = beforeValue,
            AfterValue = afterValue,
            IpAddress = ipAddress,
            OccurredAt = occurredAt.ToUniversalTime(),
        };
    }
}
