using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Application.Users;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.UnitTests.Users;

public sealed class UserUseCaseTests
{
    private const string StrongPassword = "Str0ng-Passw0rd!";

    private readonly UseCaseHarness _h = new();

    public UserUseCaseTests()
    {
        _h.Tenants.Items.Add(TenantWithId(_h.TenantId));
    }

    private static Tenant TenantWithId(Guid id)
    {
        var tenant = Tenant.Create("Phòng AI", $"t-{id:N}"[..20], UseCaseHarness.Now).Value;
        typeof(Tenant).BaseType!.GetProperty(nameof(Tenant.Id))!.SetValue(tenant, id);
        return tenant;
    }

    [Fact]
    public async Task CreateUser_TenantAdminGrantingPlatformAdmin_IsForbidden()
    {
        var result = await _h.SendAsync(new CreateUserCommand(_h.TenantId, "mallory", null, StrongPassword, [SystemRoles.PlatformAdmin]));

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("rbac.platform_admin_restricted");
        _h.Users.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateUser_PlatformAdminGrantingPlatformAdmin_IsAllowed()
    {
        _h.Actor.IsPlatformAdmin = true;

        (await _h.SendAsync(new CreateUserCommand(_h.TenantId, "ops-lead", null, StrongPassword, [SystemRoles.PlatformAdmin])))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_Viewer_AuditsWithoutPassword()
    {
        var user = (await _h.SendAsync(new CreateUserCommand(_h.TenantId, "viewer1", "v@lan.local", StrongPassword, [SystemRoles.Viewer]))).Value;

        user.Roles.Should().Equal(SystemRoles.Viewer);
        var audit = _h.AuditLogs.Should().ContainSingle().Subject;
        audit.AfterValue.Should().Contain("viewer1").And.NotContain(StrongPassword);
    }

    [Fact]
    public async Task CreateUser_UnknownRole_ReturnsValidation()
    {
        (await _h.SendAsync(new CreateUserCommand(_h.TenantId, "x-user", null, StrongPassword, ["SuperUser"])))
            .Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void CreateUserCommand_ToString_MasksPassword()
    {
        new CreateUserCommand(_h.TenantId, "u", null, StrongPassword, []).ToString().Should().NotContain(StrongPassword);
    }

    [Fact]
    public async Task SetUserRoles_TenantAdminRemovingPlatformAdmin_IsForbidden()
    {
        var target = new UserAccount(Guid.NewGuid(), _h.TenantId, "root", null, true, [SystemRoles.PlatformAdmin]);
        _h.Users.Users.Add(target);

        var result = await _h.SendAsync(new SetUserRolesCommand(_h.TenantId, target.Id, [SystemRoles.Viewer]));

        result.Error!.Code.Should().Be("rbac.platform_admin_restricted");
    }

    [Fact]
    public async Task SetUserRoles_Allowed_WritesAssignRoleAudit()
    {
        var target = new UserAccount(Guid.NewGuid(), _h.TenantId, "op", null, true, [SystemRoles.Viewer]);
        _h.Users.Users.Add(target);

        var result = await _h.SendAsync(new SetUserRolesCommand(_h.TenantId, target.Id, [SystemRoles.Operator]));

        result.Value.Roles.Should().Equal(SystemRoles.Operator);
        var audit = _h.AuditLogs.Should().ContainSingle().Subject;
        audit.Action.Should().Be(AuditAction.AssignRole);
        audit.BeforeValue.Should().Contain("Viewer");
        audit.AfterValue.Should().Contain("Operator");
    }

    [Fact]
    public async Task BootstrapPlatformAdmin_NoUsers_CreatesPlatformTenantAndAdmin()
    {
        _h.Tenants.Items.Clear();

        var result = await _h.SendAsync(new BootstrapPlatformAdminCommand("admin", null, StrongPassword));

        result.Value.Should().BeTrue();
        var tenant = _h.Tenants.Items.Should().ContainSingle(t => t.Slug == "platform").Subject;
        _h.Users.Users.Should().ContainSingle(u => u.TenantId == tenant.Id && u.Roles.Contains(SystemRoles.PlatformAdmin));
    }

    [Fact]
    public async Task BootstrapPlatformAdmin_UsersAlreadyExist_DoesNothing()
    {
        _h.Users.Users.Add(new UserAccount(Guid.NewGuid(), _h.TenantId, "existing", null, true, [SystemRoles.Viewer]));

        var result = await _h.SendAsync(new BootstrapPlatformAdminCommand("admin", null, StrongPassword));

        result.Value.Should().BeFalse();
        _h.Users.Users.Should().ContainSingle();
        _h.UnitOfWork.SaveCount.Should().Be(0);
    }
}
