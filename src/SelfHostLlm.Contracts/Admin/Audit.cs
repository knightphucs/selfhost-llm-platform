using System.Text.Json;

namespace SelfHostLlm.Contracts.Admin;

/// <summary>Một dòng audit log (chỉ đọc — không có DTO sửa/xoá).</summary>
public sealed record AuditLogResponse(
    long Id,
    Guid? ActorUserId,
    string Action,
    string EntityType,
    string EntityId,
    JsonElement? BeforeValue,
    JsonElement? AfterValue,
    string? IpAddress,
    DateTimeOffset OccurredAt);
