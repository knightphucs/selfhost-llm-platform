namespace SelfHostLlm.Domain.Access;

/// <summary>Ánh xạ mặc định role → permission; Persistence dùng để seed bảng role.</summary>
public static class RolePermissions
{
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Default { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [SystemRoles.PlatformAdmin] = Permissions.All.ToHashSet(StringComparer.Ordinal),

            [SystemRoles.TenantAdmin] = Permissions.All
                .Where(p => p != Permissions.TenantsManage)
                .ToHashSet(StringComparer.Ordinal),

            [SystemRoles.Operator] = Permissions.ReadOnly
                .Where(p => p != Permissions.AuditRead)
                .Concat(
                [
                    Permissions.ModelsWrite,
                    Permissions.ProvidersWrite,
                    Permissions.DeploymentsWrite,
                    Permissions.RoutesWrite,
                ])
                .ToHashSet(StringComparer.Ordinal),

            [SystemRoles.Viewer] = Permissions.ReadOnly.ToHashSet(StringComparer.Ordinal),
        };
}
