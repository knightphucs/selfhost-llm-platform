namespace SelfHostLlm.Contracts.Admin;

public sealed record LoginRequest(string Username, string Password)
{
    public override string ToString() => $"{nameof(LoginRequest)} {{ Username = {Username}, Password = *** }}";
}

public sealed record RefreshRequest(string RefreshToken)
{
    public override string ToString() => $"{nameof(RefreshRequest)} {{ RefreshToken = *** }}";
}

/// <summary>Thông tin người đang đăng nhập — client dùng <see cref="TenantId"/> để gọi các endpoint theo tenant.</summary>
public sealed record MeResponse(
    Guid UserId,
    Guid TenantId,
    string Username,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
