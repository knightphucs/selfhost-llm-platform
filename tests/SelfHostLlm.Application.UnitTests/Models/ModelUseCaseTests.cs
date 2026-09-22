using SelfHostLlm.Application.Models;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Models;

namespace SelfHostLlm.Application.UnitTests.Models;

public sealed class ModelUseCaseTests
{
    private readonly UseCaseHarness _h = new();

    private CreateModelCommand Create(string name = "Qwen2.5-7B", Guid? tenantId = null) =>
        new(tenantId ?? _h.TenantId, name, "qwen2.5", "7B", "Q4_K_M", 32768, ["Chat"], ["coding"]);

    [Fact]
    public async Task CreateModel_Valid_PersistsAndWritesCreateAudit()
    {
        var result = await _h.SendAsync(Create());

        result.IsSuccess.Should().BeTrue();
        _h.Repository<Model>().Items.Should().ContainSingle(m => m.Name == "Qwen2.5-7B");
        _h.UnitOfWork.SaveCount.Should().Be(1);
        var audit = _h.AuditLogs.Should().ContainSingle().Subject;
        audit.Action.Should().Be(AuditAction.Create);
        audit.EntityId.Should().Be(result.Value.Id.ToString());
        audit.BeforeValue.Should().BeNull();
        audit.AfterValue.Should().Contain("Qwen2.5-7B");
    }

    [Fact]
    public async Task CreateModel_DuplicateNameInTenant_ReturnsConflictWithoutSaving()
    {
        await _h.SendAsync(Create());

        var result = await _h.SendAsync(Create(" Qwen2.5-7B "));

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        _h.UnitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateModel_SameNameInOtherTenant_IsAllowed()
    {
        _h.Actor.IsPlatformAdmin = true;
        await _h.SendAsync(Create());

        (await _h.SendAsync(Create(tenantId: Guid.NewGuid()))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateModel_UnknownCapability_ReturnsValidationBeforeHandler()
    {
        var result = await _h.SendAsync(Create() with { Capabilities = ["Chat", "Telepathy"] });

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Details!.Keys.Should().Contain(k => k.StartsWith("Capabilities", StringComparison.Ordinal));
        _h.Repository<Model>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateModel_RecordsBeforeAndAfter()
    {
        var model = (await _h.SendAsync(Create())).Value;

        var result = await _h.SendAsync(new UpdateModelCommand(
            _h.TenantId, model.Id, "Qwen2.5-7B-Instruct", "qwen2.5", "7B", "AWQ", 4096, ["Chat"], []));

        result.Value.Quantization.Should().Be("AWQ");
        var audit = _h.AuditLogs[^1];
        audit.Action.Should().Be(AuditAction.Update);
        audit.BeforeValue.Should().Contain("Q4_K_M");
        audit.AfterValue.Should().Contain("AWQ").And.Contain("Qwen2.5-7B-Instruct");
    }

    [Fact]
    public async Task GetModel_OfOtherTenantById_ReturnsNotFound()
    {
        var foreign = Model.Create(Guid.NewGuid(), "Llama", "llama", "8B", "Q4", 8192, [ModelCapability.Chat], [], UseCaseHarness.Now).Value;
        _h.Repository<Model>().Items.Add(foreign);

        var result = await _h.SendAsync(new GetModelQuery(_h.TenantId, foreign.Id));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task DeleteModel_WhenDatabaseReportsInUse_ReturnsConflict()
    {
        var model = (await _h.SendAsync(Create())).Value;
        _h.UnitOfWork.NextError = Error.Conflict("persistence.in_use", "đang được tham chiếu");

        var result = await _h.SendAsync(new DeleteModelCommand(_h.TenantId, model.Id));

        result.Error!.Code.Should().Be("persistence.in_use");
    }

    [Fact]
    public async Task ListModels_ReturnsOnlyOwnTenantSortedByName()
    {
        await _h.SendAsync(Create("b-model"));
        await _h.SendAsync(Create("a-model"));
        _h.Repository<Model>().Items.Add(
            Model.Create(Guid.NewGuid(), "0-foreign", "x", "1B", "Q4", 1024, [ModelCapability.Chat], [], UseCaseHarness.Now).Value);

        var result = await _h.SendAsync(new ListModelsQuery(_h.TenantId));

        result.Value.Select(m => m.Name).Should().Equal("a-model", "b-model");
    }
}
