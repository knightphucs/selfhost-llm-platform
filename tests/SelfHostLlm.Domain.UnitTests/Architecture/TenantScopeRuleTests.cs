using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Domain.UnitTests.Architecture;

/// <summary>
/// Luật cô lập tenant ở mức kiểu: mọi entity mang dữ liệu người dùng phải implement
/// <see cref="ITenantScoped"/>, để repository buộc nhận <c>tenantId</c>.
/// </summary>
public sealed class TenantScopeRuleTests
{
    /// <summary>Entity duy nhất được miễn: chính Tenant là gốc cô lập.</summary>
    private static readonly Type[] Exempt = [typeof(Tenant)];

    private static IEnumerable<Type> DomainTypes =>
        DomainAssembly.Reference.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

    private static bool IsEntity(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void Entities_ExceptTenant_ShouldImplementITenantScoped()
    {
        var entities = DomainTypes.Where(IsEntity).ToList();

        entities.Should().NotBeEmpty();
        entities
            .Where(t => !Exempt.Contains(t) && !typeof(ITenantScoped).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .Should().BeEmpty("mọi entity có dữ liệu người dùng phải implement ITenantScoped");
    }

    [Fact]
    public void TypesWithTenantIdProperty_ShouldImplementITenantScoped()
    {
        DomainTypes
            .Where(t => t.GetProperty(nameof(ITenantScoped.TenantId)) is not null)
            .Where(t => !typeof(ITenantScoped).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .Should().BeEmpty("có TenantId mà không khai báo ITenantScoped sẽ lọt khỏi các kiểm tra tenant");
    }
}
