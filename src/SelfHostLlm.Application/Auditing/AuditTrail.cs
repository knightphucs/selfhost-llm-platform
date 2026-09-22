using System.Text.Json;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Application.Auditing;

/// <summary>
/// Dựng <see cref="AuditLog"/> (actor, IP, thời điểm) và thêm vào unit of work hiện tại.
/// Snapshot được serialize JSON camelCase; caller chỉ truyền snapshot từ <see cref="AuditSnapshot"/>
/// — không bao giờ truyền entity trực tiếp (có thể chứa secret).
/// </summary>
internal sealed class AuditTrail(IAuditLogWriter writer, ICurrentActor actor, TimeProvider timeProvider) : IAuditTrail
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Record(Guid tenantId, AuditAction action, string entityType, string entityId, object? before, object? after)
    {
        var auditLog = AuditLog.Create(
            tenantId,
            actor.UserId,
            action,
            entityType,
            entityId,
            Serialize(before),
            Serialize(after),
            actor.IpAddress,
            timeProvider.GetUtcNow());

        // Audit sai là lỗi lập trình (entity type rỗng...), không phải lỗi nghiệp vụ.
        writer.Add(auditLog.IsSuccess
            ? auditLog.Value
            : throw new InvalidOperationException($"Không tạo được audit log: {auditLog.Error!.Code}"));
    }

    private static string? Serialize(object? snapshot) =>
        snapshot is null ? null : JsonSerializer.Serialize(snapshot, snapshot.GetType(), JsonOptions);
}
