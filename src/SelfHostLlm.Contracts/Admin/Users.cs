namespace SelfHostLlm.Contracts.Admin;

/// <param name="Roles">Giá trị: <c>PlatformAdmin</c>, <c>TenantAdmin</c>, <c>Operator</c>, <c>Viewer</c>.</param>
public sealed record CreateUserRequest(string Username, string? Email, string Password, IReadOnlyList<string> Roles)
{
    public override string ToString() =>
        $"{nameof(CreateUserRequest)} {{ Username = {Username}, Email = {Email}, Password = ***, Roles = [{string.Join(", ", Roles)}] }}";
}

public sealed record SetUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record UserResponse(Guid Id, string Username, string? Email, bool Enabled, IReadOnlyList<string> Roles);
