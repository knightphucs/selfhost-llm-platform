using SelfHostLlm.Application.Tenants;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Validation;

public sealed class TenantUseCaseTests
{
    private readonly UseCaseHarness _h = new();

    [Fact]
    public async Task CreateTenant_ByNonPlatformAdmin_IsForbidden()
    {
        (await _h.SendAsync(new CreateTenantCommand("Phòng AI", "phong-ai"))).Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CreateTenant_ByPlatformAdmin_AuditsUnderNewTenant()
    {
        _h.Actor.IsPlatformAdmin = true;

        var tenant = (await _h.SendAsync(new CreateTenantCommand("Phòng AI", "phong-ai"))).Value;

        _h.AuditLogs.Should().ContainSingle().Which.TenantId.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task CreateTenant_DuplicateSlug_ReturnsConflict()
    {
        _h.Actor.IsPlatformAdmin = true;
        await _h.SendAsync(new CreateTenantCommand("A", "phong-ai"));

        (await _h.SendAsync(new CreateTenantCommand("B", "phong-ai"))).Error!.Code.Should().Be("tenant.slug_taken");
    }

    [Theory]
    [InlineData("Phong_AI")]
    [InlineData("x")]
    public async Task CreateTenant_InvalidSlug_ReturnsValidation(string slug)
    {
        _h.Actor.IsPlatformAdmin = true;

        (await _h.SendAsync(new CreateTenantCommand("A", slug))).Error!.Kind.Should().Be(ErrorKind.Validation);
    }
}
