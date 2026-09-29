using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Persistence.Repositories;

/// <summary>Chỉ thêm. Không có Update/Remove — và trigger ở DB chặn cả khi đi vòng.</summary>
internal sealed class AuditLogWriter(AppDbContext db) : IAuditLogWriter
{
    public void Add(AuditLog auditLog) => db.AuditLogs.Add(auditLog);
}
