using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.Persistence.Identity;

/// <summary>
/// Seed 4 role hệ thống. Id và ConcurrencyStamp cố định để migration không bị sinh lại mỗi lần.
/// Permission lấy từ <see cref="RolePermissions.Default"/> — đổi ánh xạ ở Domain thì cần migration mới.
/// </summary>
internal static class AppRoleSeed
{
    public static readonly IReadOnlyDictionary<string, Guid> RoleIds = new Dictionary<string, Guid>(StringComparer.Ordinal)
    {
        [SystemRoles.PlatformAdmin] = new("0b1d0f7e-7a51-4c3e-9f0a-000000000001"),
        [SystemRoles.TenantAdmin] = new("0b1d0f7e-7a51-4c3e-9f0a-000000000002"),
        [SystemRoles.Operator] = new("0b1d0f7e-7a51-4c3e-9f0a-000000000003"),
        [SystemRoles.Viewer] = new("0b1d0f7e-7a51-4c3e-9f0a-000000000004"),
    };

    public static IEnumerable<object> Rows() =>
        SystemRoles.All.Select(name => new
        {
            Id = RoleIds[name],
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = RoleIds[name].ToString(),
            _permissions = RolePermissions.Default[name].Order(StringComparer.Ordinal).ToList(),
        });
}
