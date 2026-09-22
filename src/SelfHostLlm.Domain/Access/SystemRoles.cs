namespace SelfHostLlm.Domain.Access;

/// <summary>Bốn role hệ thống của RBAC trên control plane.</summary>
public static class SystemRoles
{
    /// <summary>Toàn quyền trên mọi tenant.</summary>
    public const string PlatformAdmin = nameof(PlatformAdmin);

    /// <summary>Quản lý mọi thứ trong tenant của mình.</summary>
    public const string TenantAdmin = nameof(TenantAdmin);

    /// <summary>Vận hành model/provider/deployment/route, không đụng RBAC và key.</summary>
    public const string Operator = nameof(Operator);

    /// <summary>Chỉ đọc.</summary>
    public const string Viewer = nameof(Viewer);

    public static IReadOnlyList<string> All { get; } = [PlatformAdmin, TenantAdmin, Operator, Viewer];
}
