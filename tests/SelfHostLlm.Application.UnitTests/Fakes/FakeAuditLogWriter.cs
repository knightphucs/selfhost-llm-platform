using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Audit;

namespace SelfHostLlm.Application.UnitTests.Fakes;

internal sealed class FakeAuditLogWriter : IAuditLogWriter
{
    public List<AuditLog> Logs { get; } = [];

    public void Add(AuditLog auditLog) => Logs.Add(auditLog);
}
