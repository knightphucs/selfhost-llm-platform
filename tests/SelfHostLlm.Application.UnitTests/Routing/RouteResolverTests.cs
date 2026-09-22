using SelfHostLlm.Application.Routing;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.UnitTests.Routing;

public sealed class RouteResolverTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static RouteRequest Chat(string? model, string? task = null, Guid? tenant = null) =>
        new(tenant ?? TenantA, model, task, ModelCapability.Chat);

    [Fact]
    public void Resolve_VirtualModel_OrdersCandidatesByPriority()
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var pc = b.Deployment(qwen);
        var mac = b.Deployment(qwen);
        var vllm = b.Deployment(qwen);
        var codeFast = b.VirtualModel(TenantA, "code-fast", TaskKind.Coding);
        b.Route(codeFast, mac, priority: 2).Route(codeFast, pc, priority: 0).Route(codeFast, vllm, priority: 1);

        var plan = RouteResolver.Resolve(b.Build(), Chat("code-fast")).Value;

        plan.Candidates.Should().Equal(pc, vllm, mac);
        plan.VirtualModelId.Should().Be(codeFast.Id);
        plan.Task.Should().Be(TaskKind.Coding);
    }

    [Fact]
    public void Resolve_SamePriority_HigherWeightFirst()
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var light = b.Deployment(qwen);
        var heavy = b.Deployment(qwen);
        var vm = b.VirtualModel(TenantA, "chat-general");
        b.Route(vm, light, priority: 0, weight: 1).Route(vm, heavy, priority: 0, weight: 5);

        RouteResolver.Resolve(b.Build(), Chat("chat-general")).Value.Candidates.Should().Equal(heavy, light);
    }

    [Fact]
    public void Resolve_ExcludesUnhealthyAndDisabled_KeepsFallbackChainOfTheRest()
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var unhealthy = b.Deployment(qwen, HealthStatus.Unhealthy);
        var disabled = b.Deployment(qwen, enabled: false);
        var unknown = b.Deployment(qwen, HealthStatus.Unknown);
        var healthy = b.Deployment(qwen);
        var disabledRoute = b.Deployment(qwen);
        var vm = b.VirtualModel(TenantA, "code-fast");
        b.Route(vm, unhealthy, 0).Route(vm, disabled, 1).Route(vm, unknown, 2).Route(vm, healthy, 3)
            .Route(vm, disabledRoute, 4, enabled: false);

        var plan = RouteResolver.Resolve(b.Build(), Chat("code-fast")).Value;

        // Unknown (chưa probe) vẫn được thử — chỉ Unhealthy và disabled bị loại.
        plan.Candidates.Should().Equal(unknown, healthy);
    }

    [Fact]
    public void Resolve_AllDeploymentsUnhealthy_ReturnsUnavailable()
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var vm = b.VirtualModel(TenantA, "code-fast");
        b.Route(vm, b.Deployment(qwen, HealthStatus.Unhealthy), 0).Route(vm, b.Deployment(qwen, enabled: false), 1);

        var result = RouteResolver.Resolve(b.Build(), Chat("code-fast"));

        result.Error!.Kind.Should().Be(ErrorKind.Unavailable);
        result.Error.Code.Should().Be("routing.no_available_deployment");
    }

    [Fact]
    public void Resolve_SameVirtualModelNameInOtherTenant_NeverSelectsOtherTenantDeployments()
    {
        var b = new RoutingTableBuilder();
        var modelA = b.Model(TenantA, "Qwen2.5-7B");
        var modelB = b.Model(TenantB, "Qwen2.5-7B");
        var deploymentA = b.Deployment(modelA);
        var deploymentB = b.Deployment(modelB);
        b.Route(b.VirtualModel(TenantA, "code-fast"), deploymentA, 0);
        b.Route(b.VirtualModel(TenantB, "code-fast"), deploymentB, 0);
        var table = b.Build();

        RouteResolver.Resolve(table, Chat("code-fast", tenant: TenantA)).Value.Candidates.Should().Equal(deploymentA);
        RouteResolver.Resolve(table, Chat("code-fast", tenant: TenantB)).Value.Candidates.Should().Equal(deploymentB);
    }

    [Fact]
    public void Resolve_VirtualModelOnlyInOtherTenant_ReturnsNotFound()
    {
        var b = new RoutingTableBuilder();
        b.Route(b.VirtualModel(TenantB, "secret-model"), b.Deployment(b.Model(TenantB, "Qwen2.5-7B")), 0);

        RouteResolver.Resolve(b.Build(), Chat("secret-model", tenant: TenantA))
            .Error!.Code.Should().Be("routing.model_not_found");
    }

    [Fact]
    public void Resolve_RoutePointingToOtherTenantDeployment_IsIgnored()
    {
        // Snapshot hỏng/bị sửa tay: route của A trỏ sang deployment của B. Resolver vẫn không chọn.
        var b = new RoutingTableBuilder();
        var modelA = b.Model(TenantA, "Qwen2.5-7B");
        var own = b.Deployment(modelA);
        var foreign = b.Deployment(b.Model(TenantB, "Llama-3.1-8B"));
        var vm = b.VirtualModel(TenantA, "chat-general");
        b.Route(vm, foreign, 0).Route(vm, own, 1);

        RouteResolver.Resolve(b.Build(), Chat("chat-general")).Value.Candidates.Should().Equal(own);
    }

    [Fact]
    public void Resolve_DirectModelName_OrdersHealthyFirstThenByLatency()
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var unknownFast = b.Deployment(qwen, HealthStatus.Unknown, latencyMs: 10);
        var healthySlow = b.Deployment(qwen, latencyMs: 400);
        var healthyFast = b.Deployment(qwen, latencyMs: 90);
        var healthyNoData = b.Deployment(qwen, latencyMs: null);
        b.Deployment(qwen, HealthStatus.Unhealthy, latencyMs: 1);

        var plan = RouteResolver.Resolve(b.Build(), Chat("Qwen2.5-7B")).Value;

        plan.Candidates.Should().Equal(healthyFast, healthySlow, healthyNoData, unknownFast);
        plan.VirtualModelId.Should().BeNull();
    }

    [Theory]
    [InlineData("auto", "coding")]
    [InlineData(null, "Coding")]
    [InlineData("", "CODING")]
    public void Resolve_UnknownModelWithTask_UsesVirtualModelOfThatTask(string? model, string task)
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var deployment = b.Deployment(qwen);
        var codeFast = b.VirtualModel(TenantA, "code-fast", TaskKind.Coding);
        b.Route(codeFast, deployment, 0);
        b.Route(b.VirtualModel(TenantA, "chat-general", TaskKind.Chat), b.Deployment(qwen), 0);

        var plan = RouteResolver.Resolve(b.Build(), Chat(model, task)).Value;

        plan.VirtualModelId.Should().Be(codeFast.Id);
        plan.Candidates.Should().Equal(deployment);
    }

    [Fact]
    public void Resolve_KnownModelWithTask_ModelTakesPrecedence()
    {
        var b = new RoutingTableBuilder();
        var qwen = b.Model(TenantA, "Qwen2.5-7B");
        var chatDeployment = b.Deployment(qwen);
        b.Route(b.VirtualModel(TenantA, "chat-general", TaskKind.Chat), chatDeployment, 0);
        b.Route(b.VirtualModel(TenantA, "code-fast", TaskKind.Coding), b.Deployment(qwen), 0);

        RouteResolver.Resolve(b.Build(), Chat("chat-general", "coding")).Value.Candidates.Should().Equal(chatDeployment);
    }

    [Theory]
    [InlineData("translation")]
    [InlineData("1")]
    public void Resolve_InvalidTask_ReturnsValidation(string task)
    {
        RouteResolver.Resolve(new RoutingTableBuilder().Build(), Chat("auto", task))
            .Error!.Code.Should().Be("routing.task.invalid");
    }

    [Fact]
    public void Resolve_TaskWithoutConfiguredVirtualModel_ReturnsNotFound()
    {
        RouteResolver.Resolve(new RoutingTableBuilder().Build(), Chat(null, "summarization"))
            .Error!.Code.Should().Be("routing.task_not_configured");
    }

    [Fact]
    public void Resolve_UnknownModelWithoutTask_ReturnsNotFound()
    {
        var result = RouteResolver.Resolve(new RoutingTableBuilder().Build(), Chat("gpt-4o"));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("routing.model_not_found");
    }

    [Fact]
    public void Resolve_EmbeddingRequestToChatVirtualModel_ReturnsCapabilityMismatch()
    {
        var b = new RoutingTableBuilder();
        var vm = b.VirtualModel(TenantA, "chat-general");
        b.Route(vm, b.Deployment(b.Model(TenantA, "Qwen2.5-7B", ModelCapability.Chat)), 0);

        var result = RouteResolver.Resolve(b.Build(), new RouteRequest(TenantA, "chat-general", null, ModelCapability.Embedding));

        result.Error!.Code.Should().Be("routing.capability_mismatch");
    }

    [Fact]
    public void Resolve_EmbedVirtualModel_KeepsOnlyEmbeddingCapableDeployments()
    {
        var b = new RoutingTableBuilder();
        var bge = b.Model(TenantA, "bge-m3", ModelCapability.Embedding);
        var chat = b.Model(TenantA, "Qwen2.5-7B", ModelCapability.Chat);
        var embedDeployment = b.Deployment(bge);
        var vm = b.VirtualModel(TenantA, "embed", TaskKind.Embedding);
        b.Route(vm, b.Deployment(chat), 0).Route(vm, embedDeployment, 1);

        RouteResolver.Resolve(b.Build(), new RouteRequest(TenantA, "embed", null, ModelCapability.Embedding))
            .Value.Candidates.Should().Equal(embedDeployment);
    }
}
