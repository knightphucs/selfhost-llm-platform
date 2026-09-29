using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Security;

namespace SelfHostLlm.Persistence.Identity;

public static class IdentityBuilderExtensions
{
    /// <summary>
    /// Store Identity trên <see cref="AppDbContext"/>: store không tự SaveChanges (để user và audit
    /// lưu chung một transaction), token mang claim tenant + permission, user bị tắt không đăng nhập được.
    /// </summary>
    public static IdentityBuilder AddPlatformStores(this IdentityBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEntityFrameworkStores<AppDbContext>()
            .AddUserStore<AppUserStore>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();
        builder.Services.AddScoped<IUserConfirmation<AppUser>, EnabledUserConfirmation>();

        // Cần UserManager nên đăng ký cùng Identity — host không có Identity (Gateway) không thấy nó.
        builder.Services.AddScoped<IUserDirectory, UserDirectory>();
        return builder;
    }
}

/// <summary>Tắt AutoSaveChanges: thay đổi user được lưu bởi <c>IUnitOfWork</c> cùng audit log.</summary>
internal sealed class AppUserStore : UserStore<AppUser, AppRole, AppDbContext, Guid>
{
    public AppUserStore(AppDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        AutoSaveChanges = false;
    }

    /// <summary>
    /// Store gốc giả định mỗi lần Update đều lưu ngay: nó gọi <c>Context.Update(user)</c>. Khi không
    /// tự lưu, gọi lại trên user đã Added sẽ biến thành Modified (UPDATE dòng chưa tồn tại), và gọi
    /// lần hai trên user đã Modified làm EF ghi đè giá trị gốc của concurrency stamp (UPDATE ảnh
    /// hưởng 0 dòng). Vì vậy chỉ gọi store gốc khi user đang Unchanged; còn lại chỉ đổi stamp.
    /// </summary>
    public override Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (Context.Entry(user).State is EntityState.Added or EntityState.Modified)
        {
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            return Task.FromResult(IdentityResult.Success);
        }

        return base.UpdateAsync(user, cancellationToken);
    }
}

/// <summary>Dùng cơ chế "confirmed account" của Identity để chặn đăng nhập của user bị tắt.</summary>
internal sealed class EnabledUserConfirmation : IUserConfirmation<AppUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<AppUser> manager, AppUser user) => Task.FromResult(user.Enabled);
}

/// <summary>Token chứa <c>tenant_id</c> và một claim <c>permission</c> cho mỗi quyền gộp từ các role.</summary>
internal sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, AppRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(PlatformClaims.TenantId, user.TenantId.ToString()));

        var roleNames = await UserManager.GetRolesAsync(user);
        var permissionLists = await RoleManager.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => EF.Property<List<string>>(r, "_permissions"))
            .ToListAsync();

        foreach (var permission in permissionLists.SelectMany(p => p).Distinct(StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim(PlatformClaims.Permission, permission));
        }

        return identity;
    }
}
