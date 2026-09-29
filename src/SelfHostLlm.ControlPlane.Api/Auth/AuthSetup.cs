using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Persistence.Identity;

namespace SelfHostLlm.ControlPlane.Api.Auth;

internal static class AuthSetup
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Identity + bearer token (opaque, mã hoá bằng Data Protection). Không map endpoint đăng ký:
    /// user chỉ được tạo bởi admin có <c>rbac:manage</c>. Mọi endpoint mặc định yêu cầu đăng nhập,
    /// mỗi permission là một policy.
    /// </summary>
    public static IServiceCollection AddPlatformAuth(this IServiceCollection services)
    {
        services.AddIdentityApiEndpoints<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // "Confirmed" = user đang bật (xem EnabledUserConfirmation).
                options.SignIn.RequireConfirmedAccount = true;
            })
            .AddRoles<AppRole>()
            .AddPlatformStores();

        // Chỉ dùng bearer — không cookie, nên API không bao giờ redirect 302 tới trang login.
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.BearerScheme;
            options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
        });
        services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = AccessTokenLifetime;
            options.RefreshTokenExpiration = RefreshTokenLifetime;
        });

        var authorization = services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(IdentityConstants.BearerScheme).RequireAuthenticatedUser().Build());
        foreach (var permission in Permissions.All)
        {
            authorization.AddPolicy(permission, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.BearerScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(PlatformClaims.Permission, permission));
        }

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        return services;
    }
}
