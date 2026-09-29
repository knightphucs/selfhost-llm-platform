using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Quản lý user quản trị. Implementation dựa trên ASP.NET Core Identity và KHÔNG tự lưu —
/// thay đổi được lưu cùng audit log qua <see cref="IUnitOfWork"/>.
/// </summary>
public interface IUserDirectory
{
    Task<bool> AnyUserAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAccount>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<UserAccount?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Tạo user; mật khẩu không đạt chính sách → <see cref="ErrorKind.Validation"/> kèm chi tiết.</summary>
    Task<Result<UserAccount>> CreateAsync(
        Guid tenantId,
        string username,
        string? email,
        string password,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);

    Task<Result<UserAccount>> SetRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
}

/// <summary>User quản trị — không có password hash hay security stamp.</summary>
public sealed record UserAccount(Guid Id, Guid TenantId, string Username, string? Email, bool Enabled, IReadOnlyList<string> Roles);
