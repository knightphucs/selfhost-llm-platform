using Microsoft.AspNetCore.Identity;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Persistence.Identity;

/// <summary>
/// Người dùng quản trị (bảng <c>app_user</c>). Nằm ở Persistence vì kế thừa Identity —
/// Domain không được phụ thuộc Identity.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>, ITenantScoped
{
    public Guid TenantId { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
