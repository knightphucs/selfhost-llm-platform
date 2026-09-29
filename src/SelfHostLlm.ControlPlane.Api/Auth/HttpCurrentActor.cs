using System.Security.Claims;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.Api.Auth;

/// <summary>Actor lấy từ claims của bearer token. Ngoài request (job nền) thì là actor hệ thống.</summary>
internal sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid TenantId => Guid.TryParse(User?.FindFirstValue(PlatformClaims.TenantId), out var id) ? id : Guid.Empty;

    public bool IsPlatformAdmin => User?.IsInRole(SystemRoles.PlatformAdmin) == true;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
