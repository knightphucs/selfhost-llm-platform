using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.Users;

public sealed record CreateUserCommand(Guid TenantId, string Username, string? Email, string Password, IReadOnlyList<string> Roles)
    : IRequest<Result<UserAccount>>, ITenantRequest
{
    public override string ToString() =>
        $"{nameof(CreateUserCommand)} {{ TenantId = {TenantId}, Username = {Username}, Password = ***, Roles = [{string.Join(", ", Roles)}] }}";
}

/// <summary>Thay toàn bộ role của user (ghi audit <see cref="AuditAction.AssignRole"/>).</summary>
public sealed record SetUserRolesCommand(Guid TenantId, Guid UserId, IReadOnlyList<string> Roles)
    : IRequest<Result<UserAccount>>, ITenantRequest;

public sealed record ListUsersQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<UserAccount>>>, ITenantRequest;

internal static class RoleRules
{
    public static IRuleBuilderOptions<T, IReadOnlyList<string>> ValidRoles<T>(this IRuleBuilder<T, IReadOnlyList<string>> rule) =>
        rule.NotEmpty()
            .Must(roles => roles.All(r => SystemRoles.All.Contains(r)))
            .WithMessage($"Role hợp lệ: {string.Join(", ", SystemRoles.All)}.");

    /// <summary>
    /// Chặn leo thang đặc quyền: chỉ PlatformAdmin được trao hoặc tước role PlatformAdmin —
    /// TenantAdmin có <c>rbac:manage</c> cũng không tự nâng mình (hay người khác) lên toàn quyền.
    /// </summary>
    public static Error? GuardPlatformAdminRole(ICurrentActor actor, IEnumerable<string> touchedRoles) =>
        !actor.IsPlatformAdmin && touchedRoles.Contains(SystemRoles.PlatformAdmin)
            ? Error.Forbidden("rbac.platform_admin_restricted", "Chỉ PlatformAdmin được trao hoặc tước role PlatformAdmin.")
            : null;
}

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Username).NotEmpty().Matches("^[a-zA-Z0-9._@-]{3,64}$")
            .WithMessage("Username 3–64 ký tự: chữ, số, '.', '_', '@', '-'.");
        RuleFor(c => c.Email).EmailAddress().MaximumLength(256).When(c => c.Email is not null);
        RuleFor(c => c.Password).NotEmpty();
        RuleFor(c => c.Roles).ValidRoles();
    }
}

internal sealed class SetUserRolesValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Roles).ValidRoles();
    }
}

internal sealed class CreateUserHandler(
    IUserDirectory users,
    ITenantStore tenants,
    ICurrentActor actor,
    IAuditTrail audit,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateUserCommand, Result<UserAccount>>
{
    public async Task<Result<UserAccount>> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (RoleRules.GuardPlatformAdminRole(actor, request.Roles) is { } forbidden)
        {
            return forbidden;
        }

        if (await tenants.GetAsync(request.TenantId, cancellationToken) is null)
        {
            return UseCaseExtensions.NotFound<Tenant>(request.TenantId);
        }

        var created = await users.CreateAsync(request.TenantId, request.Username, request.Email, request.Password, request.Roles, cancellationToken);
        if (created.IsFailure)
        {
            return created;
        }

        audit.Record(request.TenantId, AuditAction.Create, "AppUser", created.Value.Id.ToString(), null, AuditSnapshot.Of(created.Value));
        return await unitOfWork.SaveAndReturnAsync(created.Value, cancellationToken);
    }
}

internal sealed class SetUserRolesHandler(IUserDirectory users, ICurrentActor actor, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<SetUserRolesCommand, Result<UserAccount>>
{
    public async Task<Result<UserAccount>> HandleAsync(SetUserRolesCommand request, CancellationToken cancellationToken)
    {
        if (await users.GetAsync(request.TenantId, request.UserId, cancellationToken) is not { } before)
        {
            return Error.NotFound("appuser.not_found", $"Không tìm thấy user {request.UserId}.");
        }

        // Cả role được thêm lẫn role bị bỏ đều tính là "chạm tới".
        var touched = request.Roles.Except(before.Roles).Concat(before.Roles.Except(request.Roles));
        if (RoleRules.GuardPlatformAdminRole(actor, touched) is { } forbidden)
        {
            return forbidden;
        }

        var updated = await users.SetRolesAsync(request.TenantId, request.UserId, request.Roles, cancellationToken);
        if (updated.IsFailure)
        {
            return updated;
        }

        audit.Record(request.TenantId, AuditAction.AssignRole, "AppUser", request.UserId.ToString(), AuditSnapshot.Of(before), AuditSnapshot.Of(updated.Value));
        return await unitOfWork.SaveAndReturnAsync(updated.Value, cancellationToken);
    }
}

internal sealed class ListUsersHandler(IUserDirectory users) : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserAccount>>>
{
    public async Task<Result<IReadOnlyList<UserAccount>>> HandleAsync(ListUsersQuery request, CancellationToken cancellationToken) =>
        Result<IReadOnlyList<UserAccount>>.Success(await users.ListAsync(request.TenantId, cancellationToken));
}
