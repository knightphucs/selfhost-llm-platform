using NetArchTest.Rules;
using SelfHostLlm.Contracts;
using SelfHostLlm.Domain;

namespace SelfHostLlm.Domain.UnitTests.Architecture;

/// <summary>
/// Khẳng định quy tắc phụ thuộc Clean Architecture trong CLAUDE.md:
/// Domain không tham chiếu project nào, Contracts không tham chiếu Domain.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] OtherSolutionProjects =
    [
        "SelfHostLlm.Application",
        "SelfHostLlm.Contracts",
        "SelfHostLlm.Persistence",
        "SelfHostLlm.Adapters.Inference",
        "SelfHostLlm.ControlPlane.Api",
        "SelfHostLlm.Gateway",
        "SelfHostLlm.Worker.Health",
    ];

    [Fact]
    public void DomainAssembly_ShouldNot_DependOnAnyOtherSolutionProject()
    {
        var result = Types.InAssembly(DomainAssembly.Reference)
            .ShouldNot()
            .HaveDependencyOnAny(OtherSolutionProjects)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(because: Describe(result));
        DomainAssembly.Reference.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(name => OtherSolutionProjects.Contains(name));
    }

    [Fact]
    public void ContractsAssembly_ShouldNot_DependOnDomain()
    {
        var result = Types.InAssembly(ContractsAssembly.Reference)
            .ShouldNot()
            .HaveDependencyOn("SelfHostLlm.Domain")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(because: Describe(result));
        ContractsAssembly.Reference.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain("SelfHostLlm.Domain");
    }

    [Fact]
    public void DomainAssembly_ShouldNot_DependOnEntityFrameworkOrAspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly.Reference)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(because: Describe(result));
    }

    private static string Describe(TestResult result) =>
        "các type vi phạm: " + string.Join(", ", result.FailingTypeNames ?? []);
}
