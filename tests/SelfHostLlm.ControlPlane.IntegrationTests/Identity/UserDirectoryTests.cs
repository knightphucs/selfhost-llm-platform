using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Security;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Persistence;
using SelfHostLlm.Persistence.Identity;
using SelfHostLlm.Persistence.Repositories;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Identity;

[Collection(PostgresDatabase.Name)]
public sealed class UserDirectoryTests(PostgresFixture fixture)
{
    private const string StrongPassword = "Str0ng-Passw0rd!";

    private IServiceScope NewScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(o => AppDbContextOptions.Configure(o, fixture.DataSource));
        services.AddIdentityCore<AppUser>(o => o.Password.RequiredLength = 10).AddRoles<AppRole>().AddPlatformStores();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services.BuildServiceProvider().CreateScope();
    }

    private async Task<Guid> NewTenantAsync()
    {
        var tenant = TestData.Tenant();
        await using var db = fixture.CreateDbContext();
        await TestData.SaveAsync(db, tenant);
        return tenant.Id;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task CreateAsync_ThenSave_PersistsUserWithRoles()
    {
        var tenantId = await NewTenantAsync();
        using var scope = NewScope();
        var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
        var username = Unique("operator");

        var created = await directory.CreateAsync(tenantId, username, null, StrongPassword, [SystemRoles.Operator], CancellationToken.None);
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();

        created.IsSuccess.Should().BeTrue();
        using var readScope = NewScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<IUserDirectory>().GetAsync(tenantId, created.Value.Id, CancellationToken.None);
        loaded!.Roles.Should().Equal(SystemRoles.Operator);
        loaded.Username.Should().Be(username);
    }

    [Fact]
    public async Task CreateAsync_WithoutSave_PersistsNothing()
    {
        var tenantId = await NewTenantAsync();
        using var scope = NewScope();

        await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
            .CreateAsync(tenantId, Unique("ghost"), null, StrongPassword, [SystemRoles.Viewer], CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        (await db.Users.AnyAsync(u => u.TenantId == tenantId)).Should().BeFalse("store không tự lưu — user và audit lưu chung một transaction");
    }

    [Fact]
    public async Task CreateAsync_WeakPassword_ReturnsValidationWithPasswordDetails()
    {
        var tenantId = await NewTenantAsync();
        using var scope = NewScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
            .CreateAsync(tenantId, Unique("weak"), null, "short", [SystemRoles.Viewer], CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Details.Should().ContainKey("Password");
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_ReturnsConflict()
    {
        var tenantId = await NewTenantAsync();
        var username = Unique("dup");
        using (var scope = NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
                .CreateAsync(tenantId, username, null, StrongPassword, [SystemRoles.Viewer], CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        using var second = NewScope();
        var result = await second.ServiceProvider.GetRequiredService<IUserDirectory>()
            .CreateAsync(tenantId, username, null, StrongPassword, [SystemRoles.Viewer], CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public async Task ClaimsPrincipal_ContainsTenantAndPermissionsOfRoles()
    {
        var tenantId = await NewTenantAsync();
        Guid userId;
        using (var scope = NewScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
                .CreateAsync(tenantId, Unique("op"), null, StrongPassword, [SystemRoles.Operator], CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
            userId = created.Value.Id;
        }

        using var readScope = NewScope();
        var userManager = readScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var factory = readScope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
        var principal = await factory.CreateAsync((await userManager.FindByIdAsync(userId.ToString()))!);

        principal.FindFirstValue(PlatformClaims.TenantId).Should().Be(tenantId.ToString());
        principal.FindAll(PlatformClaims.Permission).Select(c => c.Value)
            .Should().BeEquivalentTo(RolePermissions.Default[SystemRoles.Operator]);
        principal.IsInRole(SystemRoles.Operator).Should().BeTrue();
    }

    [Fact]
    public async Task SetRolesAsync_ReplacesRoles()
    {
        var tenantId = await NewTenantAsync();
        Guid userId;
        using (var scope = NewScope())
        {
            userId = (await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
                .CreateAsync(tenantId, Unique("r"), null, StrongPassword, [SystemRoles.Viewer], CancellationToken.None)).Value.Id;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = NewScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IUserDirectory>()
                .SetRolesAsync(tenantId, userId, [SystemRoles.Operator, SystemRoles.TenantAdmin], CancellationToken.None)).IsSuccess.Should().BeTrue();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        using var readScope = NewScope();
        (await readScope.ServiceProvider.GetRequiredService<IUserDirectory>().GetAsync(tenantId, userId, CancellationToken.None))!
            .Roles.Should().BeEquivalentTo([SystemRoles.Operator, SystemRoles.TenantAdmin]);
    }
}
