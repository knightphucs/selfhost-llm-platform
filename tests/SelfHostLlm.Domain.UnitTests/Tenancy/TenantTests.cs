using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Domain.UnitTests.Tenancy;

public sealed class TenantTests
{
    [Fact]
    public void Create_WithValidSlug_Succeeds()
    {
        Tenant.Create("Phòng Kỹ thuật", "phong-ky-thuat", TestClock.Now).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Phong_KT")]
    [InlineData("")]
    public void Create_WithInvalidSlug_ReturnsValidationError(string slug)
    {
        Tenant.Create("Tenant", slug, TestClock.Now).Error!.Code.Should().Be("tenant.slug.invalid");
    }
}
