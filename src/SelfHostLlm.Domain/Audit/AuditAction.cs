namespace SelfHostLlm.Domain.Audit;

public enum AuditAction
{
    Create,
    Update,
    Delete,
    Revoke,
    Enable,
    Disable,
    AssignRole,
    Login,
}
