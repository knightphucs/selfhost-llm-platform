using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Persistence.Identity;

/// <summary>
/// <see cref="IUserDirectory"/> trên ASP.NET Core Identity. Mọi truy vấn lọc theo tenant.
/// Store không tự lưu — caller gọi <c>IUnitOfWork.SaveChangesAsync</c>.
/// </summary>
internal sealed class UserDirectory(UserManager<AppUser> userManager, AppDbContext db, TimeProvider timeProvider) : IUserDirectory
{
    public Task<bool> AnyUserAsync(CancellationToken cancellationToken) => db.Users.AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<UserAccount>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);
        var roles = await RolesOfAsync(users.Select(u => u.Id).ToList(), cancellationToken);
        return users.Select(u => ToAccount(u, roles[u.Id].ToList())).ToList();
    }

    public async Task<UserAccount?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);
        return user is null ? null : ToAccount(user, (await RolesOfAsync([user.Id], cancellationToken))[user.Id].ToList());
    }

    public async Task<Result<UserAccount>> CreateAsync(
        Guid tenantId,
        string username,
        string? email,
        string password,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = username,
            Email = email,
            Enabled = true,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return ToError(created);
        }

        var assigned = await userManager.AddToRolesAsync(user, roles);
        return assigned.Succeeded ? ToAccount(user, roles.ToList()) : ToError(assigned);
    }

    public async Task<Result<UserAccount>> SetRolesAsync(
        Guid tenantId,
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("appuser.not_found", $"Không tìm thấy user {userId}.");
        }

        var current = await userManager.GetRolesAsync(user);
        var removed = await userManager.RemoveFromRolesAsync(user, current.Except(roles));
        if (!removed.Succeeded)
        {
            return ToError(removed);
        }

        var added = await userManager.AddToRolesAsync(user, roles.Except(current));
        return added.Succeeded ? ToAccount(user, roles.ToList()) : ToError(added);
    }

    private async Task<ILookup<Guid, string>> RolesOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var pairs = await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                orderby role.Name
                select new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);
        return pairs.ToLookup(p => p.UserId, p => p.RoleName);
    }

    private static UserAccount ToAccount(AppUser user, IReadOnlyList<string> roles) =>
        new(user.Id, user.TenantId, user.UserName!, user.Email, user.Enabled, roles);

    /// <summary>Lỗi Identity → lỗi nghiệp vụ: trùng username là Conflict, còn lại là Validation theo field.</summary>
    private static Error ToError(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return Error.Conflict("appuser.duplicate", "Username hoặc email đã được dùng.");
        }

        var details = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password"
                : e.Code.Contains("UserName", StringComparison.Ordinal) ? "Username"
                : e.Code.Contains("Email", StringComparison.Ordinal) ? "Email"
                : e.Code.Contains("Role", StringComparison.Ordinal) ? "Roles"
                : "User")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray(), StringComparer.Ordinal);
        return Error.Validation("identity.invalid", "Thông tin user không hợp lệ.", details);
    }
}
