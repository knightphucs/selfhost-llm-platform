using SelfHostLlm.Application.Deployments;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;

namespace SelfHostLlm.Application.UnitTests.Deployments;

public sealed class DeploymentUseCaseTests
{
    private const string EngineKey = "engine-secret-token-123";

    private readonly UseCaseHarness _h = new();
    private readonly Model _model;
    private readonly Provider _provider;

    public DeploymentUseCaseTests()
    {
        _model = Model.Create(_h.TenantId, "Qwen2.5-7B", "qwen2.5", "7B", "Q4_K_M", 32768, [ModelCapability.Chat], [], UseCaseHarness.Now).Value;
        _provider = Provider.Create(_h.TenantId, "Ollama PC", ProviderKind.Ollama, null).Value;
        _h.Repository<Model>().Items.Add(_model);
        _h.Repository<Provider>().Items.Add(_provider);
    }

    private CreateDeploymentCommand Create(string? apiKey = null, Guid? modelId = null, Guid? providerId = null) =>
        new(_h.TenantId, modelId ?? _model.Id, null, providerId ?? _provider.Id, "http://192.168.1.50:11434/", "qwen2.5:7b-instruct-q4_K_M", apiKey);

    [Fact]
    public async Task CreateDeployment_WithEngineKey_StoresOnlyEncryptedKey()
    {
        var deployment = (await _h.SendAsync(Create(EngineKey))).Value;

        deployment.ApiKeyEncrypted.Should().NotBeNull().And.NotBe(EngineKey).And.StartWith("ENC:");
        new FakeSecretProtector().Unprotect(deployment.ApiKeyEncrypted!).Should().Be(EngineKey);
        deployment.HealthStatus.Should().Be(HealthStatus.Unknown);
        deployment.Address.ToString().Should().Be("http://192.168.1.50:11434");
    }

    [Fact]
    public async Task CreateDeployment_AuditSnapshot_ContainsNeitherKeyNorCiphertext()
    {
        var deployment = (await _h.SendAsync(Create(EngineKey))).Value;

        var audit = _h.AuditLogs.Should().ContainSingle().Subject;
        audit.AfterValue.Should().NotContain(EngineKey)
            .And.NotContain(deployment.ApiKeyEncrypted!)
            .And.Contain("\"hasApiKey\":true");
    }

    [Fact]
    public void CreateDeploymentCommand_ToString_MasksEngineKey()
    {
        Create(EngineKey).ToString().Should().NotContain(EngineKey);
    }

    [Fact]
    public async Task CreateDeployment_ModelOfOtherTenant_ReturnsNotFound()
    {
        var foreignModel = Model.Create(Guid.NewGuid(), "Llama", "llama", "8B", "Q4", 8192, [ModelCapability.Chat], [], UseCaseHarness.Now).Value;
        _h.Repository<Model>().Items.Add(foreignModel);

        var result = await _h.SendAsync(Create(modelId: foreignModel.Id));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        _h.Repository<Deployment>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeployment_ProviderOfOtherTenant_ReturnsNotFound()
    {
        var foreignProvider = Provider.Create(Guid.NewGuid(), "x", ProviderKind.Vllm, null).Value;
        _h.Repository<Provider>().Items.Add(foreignProvider);

        (await _h.SendAsync(Create(providerId: foreignProvider.Id))).Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task CreateDeployment_SameProviderAddressAndRemoteName_ReturnsConflict()
    {
        await _h.SendAsync(Create());

        (await _h.SendAsync(Create())).Error!.Code.Should().Be("deployment.duplicate");
    }

    [Theory]
    [InlineData("192.168.1.50:11434")]
    [InlineData("ftp://192.168.1.50")]
    public async Task CreateDeployment_InvalidBaseUrl_ReturnsValidation(string url)
    {
        (await _h.SendAsync(Create() with { BaseUrl = url })).Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task SetDeploymentEnabled_Disable_WritesDisableAudit()
    {
        var deployment = (await _h.SendAsync(Create())).Value;

        var result = await _h.SendAsync(new SetDeploymentEnabledCommand(_h.TenantId, deployment.Id, false));

        result.Value.Enabled.Should().BeFalse();
        _h.AuditLogs[^1].Action.Should().Be(AuditAction.Disable);
    }

    [Fact]
    public async Task SetDeploymentApiKey_Null_RemovesKeyAndAuditsWithoutSecret()
    {
        var deployment = (await _h.SendAsync(Create(EngineKey))).Value;

        await _h.SendAsync(new SetDeploymentApiKeyCommand(_h.TenantId, deployment.Id, null));

        deployment.ApiKeyEncrypted.Should().BeNull();
        var audit = _h.AuditLogs[^1];
        audit.BeforeValue.Should().Contain("\"hasApiKey\":true").And.NotContain("ENC:");
        audit.AfterValue.Should().Contain("\"hasApiKey\":false");
    }

    [Fact]
    public async Task CreateRoute_DeploymentOfOtherTenant_ReturnsNotFound()
    {
        var virtualModel = (await _h.SendAsync(new CreateVirtualModelCommand(_h.TenantId, "code-fast", "Coding", null))).Value;
        var foreignDeployment = Deployment.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            Address.Create("http://10.0.0.9:8000").Value, "x", null).Value;
        _h.Repository<Deployment>().Items.Add(foreignDeployment);

        var result = await _h.SendAsync(new CreateRouteCommand(_h.TenantId, virtualModel.Id, foreignDeployment.Id, 0));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        _h.Repository<Route>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListRoutes_ReturnsFallbackOrder()
    {
        var virtualModel = (await _h.SendAsync(new CreateVirtualModelCommand(_h.TenantId, "code-fast", "coding", null))).Value;
        var first = (await _h.SendAsync(Create())).Value;
        var second = (await _h.SendAsync(Create() with { RemoteModelName = "other" })).Value;
        await _h.SendAsync(new CreateRouteCommand(_h.TenantId, virtualModel.Id, second.Id, Priority: 1));
        await _h.SendAsync(new CreateRouteCommand(_h.TenantId, virtualModel.Id, first.Id, Priority: 0));

        var routes = (await _h.SendAsync(new ListRoutesQuery(_h.TenantId, virtualModel.Id))).Value;

        routes.Select(r => r.DeploymentId).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateVirtualModel_InvalidTask_ReturnsValidation()
    {
        (await _h.SendAsync(new CreateVirtualModelCommand(_h.TenantId, "code-fast", "translation", null)))
            .Error!.Kind.Should().Be(ErrorKind.Validation);
    }
}
