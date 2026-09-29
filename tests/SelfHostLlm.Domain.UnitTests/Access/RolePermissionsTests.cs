using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.Domain.UnitTests.Access;

public sealed class RolePermissionsTests
{
    [Fact]
    public void Default_CoversEverySystemRole()
    {
        RolePermissions.Default.Keys.Should().BeEquivalentTo(SystemRoles.All);
    }

    [Fact]
    public void Default_OnlyPlatformAdminCanManageTenants()
    {
        RolePermissions.Default
            .Where(kv => kv.Value.Contains(Permissions.TenantsManage))
            .Select(kv => kv.Key)
            .Should().Equal(SystemRoles.PlatformAdmin);
    }

    [Fact]
    public void Default_ViewerHasOnlyReadPermissions()
    {
        RolePermissions.Default[SystemRoles.Viewer]
            .Should().OnlyContain(p => p.EndsWith(":read", StringComparison.Ordinal));
    }

    [Fact]
    public void Default_OperatorCannotTouchRbacOrApiKeys()
    {
        RolePermissions.Default[SystemRoles.Operator]
            .Should().NotContain([Permissions.RbacManage, Permissions.ApiKeysManage, Permissions.QuotasWrite])
            .And.Contain(Permissions.DeploymentsWrite);
    }

    [Fact]
    public void Permissions_All_HasNoDuplicates()
    {
        Permissions.All.Should().OnlyHaveUniqueItems();
    }
}
