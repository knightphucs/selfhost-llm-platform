using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Persistence.Identity;

namespace SelfHostLlm.ControlPlane.Api.Endpoints;

/// <summary>
/// Đăng nhập bằng bearer token của Identity. Chỉ có login / refresh / me — KHÔNG có đăng ký:
/// user do admin có <c>rbac:manage</c> tạo.
/// </summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        group.MapGet("/me", Me);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<AppUser> signInManager,
        IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var user = await signInManager.UserManager.FindByNameAsync(request.Username);
        if (user is null)
        {
            return InvalidCredentials();
        }

        // Kiểm mật khẩu + khoá tạm + user bị tắt, rồi mới ghi audit và phát token.
        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return check.IsLockedOut
                ? TypedResults.Problem("Tài khoản tạm bị khoá do đăng nhập sai nhiều lần.", statusCode: StatusCodes.Status401Unauthorized,
                    extensions: new Dictionary<string, object?> { ["code"] = "auth.locked_out" })
                : InvalidCredentials();
        }

        await dispatcher.SendAsync(new RecordSignInCommand(user.TenantId, user.Id), cancellationToken);

        // Bearer handler tự ghi { accessToken, refreshToken, expiresIn } vào response.
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        await signInManager.SignInAsync(user, isPersistent: false);
        return TypedResults.Empty;
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        SignInManager<AppUser> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerOptions,
        TimeProvider timeProvider)
    {
        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc
            || timeProvider.GetUtcNow() >= expiresUtc
            || await signInManager.ValidateSecurityStampAsync(ticket.Principal) is not AppUser user
            || !user.Enabled)
        {
            return TypedResults.Challenge();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static Ok<MeResponse> Me(ClaimsPrincipal user) => TypedResults.Ok(new MeResponse(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Guid.Parse(user.FindFirstValue(PlatformClaims.TenantId)!),
        user.Identity!.Name!,
        user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
        user.FindAll(PlatformClaims.Permission).Select(c => c.Value).Order(StringComparer.Ordinal).ToList()));

    private static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem("Sai username hoặc mật khẩu, hoặc tài khoản đã bị tắt.", statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?> { ["code"] = "auth.invalid_credentials" });
}
