using Microsoft.AspNetCore.Identity;

namespace SelfHostLlm.Persistence.Identity;

/// <summary>Role RBAC (bảng <c>role</c>). Policy kiểm tra theo <see cref="Permissions"/>, không theo tên role.</summary>
public sealed class AppRole : IdentityRole<Guid>
{
    private List<string> _permissions = [];

    public IReadOnlyList<string> Permissions => _permissions;

    public void SetPermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions = permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }
}
