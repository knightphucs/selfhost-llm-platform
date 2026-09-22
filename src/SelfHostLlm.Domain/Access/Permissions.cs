namespace SelfHostLlm.Domain.Access;

/// <summary>
/// Permission string dạng <c>resource:action</c>. Policy ở ControlPlane.Api kiểm tra theo
/// permission, không theo tên role — đổi quyền của role không phải sửa code endpoint.
/// </summary>
public static class Permissions
{
    public const string TenantsManage = "tenants:manage";
    public const string RbacManage = "rbac:manage";

    public const string ModelsRead = "models:read";
    public const string ModelsWrite = "models:write";
    public const string ProvidersRead = "providers:read";
    public const string ProvidersWrite = "providers:write";
    public const string DeploymentsRead = "deployments:read";
    public const string DeploymentsWrite = "deployments:write";
    public const string RoutesRead = "routes:read";
    public const string RoutesWrite = "routes:write";

    public const string ConsumersRead = "consumers:read";
    public const string ConsumersWrite = "consumers:write";
    public const string ApiKeysManage = "apikeys:manage";
    public const string QuotasRead = "quotas:read";
    public const string QuotasWrite = "quotas:write";

    public const string UsageRead = "usage:read";
    public const string AuditRead = "audit:read";

    public const string RagRead = "rag:read";
    public const string RagWrite = "rag:write";
    public const string TrainingRead = "training:read";
    public const string TrainingWrite = "training:write";

    public static IReadOnlyList<string> All { get; } =
    [
        TenantsManage, RbacManage,
        ModelsRead, ModelsWrite, ProvidersRead, ProvidersWrite,
        DeploymentsRead, DeploymentsWrite, RoutesRead, RoutesWrite,
        ConsumersRead, ConsumersWrite, ApiKeysManage, QuotasRead, QuotasWrite,
        UsageRead, AuditRead,
        RagRead, RagWrite, TrainingRead, TrainingWrite,
    ];

    /// <summary>Mọi permission chỉ đọc (<c>:read</c>).</summary>
    public static IReadOnlyList<string> ReadOnly { get; } =
        All.Where(p => p.EndsWith(":read", StringComparison.Ordinal)).ToArray();
}
