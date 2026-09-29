namespace SelfHostLlm.Application.Security;

/// <summary>Claim type dùng chung giữa nơi phát token (Persistence) và nơi kiểm quyền (host).</summary>
public static class PlatformClaims
{
    public const string TenantId = "tenant_id";

    /// <summary>Một claim cho mỗi permission (<c>models:write</c>...) — gộp từ mọi role của user.</summary>
    public const string Permission = "permission";
}
