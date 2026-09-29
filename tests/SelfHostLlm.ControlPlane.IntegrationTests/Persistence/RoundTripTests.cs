using Microsoft.EntityFrameworkCore;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Training;
using SelfHostLlm.Domain.Usage;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Persistence;

/// <summary>Lưu rồi đọc lại bằng DbContext mới — xác nhận converter và kiểu cột đúng.</summary>
[Collection(PostgresDatabase.Name)]
public sealed class RoundTripTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Model_WithCapabilitiesAndTags_RoundTrips()
    {
        var tenant = TestData.Tenant();
        var model = Model.Create(
            tenant.Id, $"llava-{Guid.NewGuid():N}", "llava", "7B", "Q4_K_M", 4096,
            [ModelCapability.Chat, ModelCapability.Vision], ["vision", "chat"], TestData.Now).Value;
        await using (var db = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(db, tenant, model);
        }

        await using var read = fixture.CreateDbContext();
        var loaded = await read.Models.SingleAsync(m => m.Id == model.Id);

        loaded.Capabilities.Should().Equal(ModelCapability.Chat, ModelCapability.Vision);
        loaded.TaskTags.Should().Equal("vision", "chat");
        loaded.CreatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task Deployment_WithAddressAndHealth_RoundTripsInUtc()
    {
        var tenant = TestData.Tenant();
        var model = TestData.ChatModel(tenant.Id);
        var provider = TestData.Provider(tenant.Id);
        var deployment = TestData.Deployment(tenant.Id, model.Id, provider.Id);
        var probedAt = new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.FromHours(7));
        deployment.RecordProbeSuccess(120, probedAt);
        deployment.SetApiKeyEncrypted("CfDJ8-ciphertext");
        await using (var db = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(db, tenant, model, provider, deployment);
        }

        await using var read = fixture.CreateDbContext();
        var loaded = await read.Deployments.SingleAsync(d => d.Id == deployment.Id);

        loaded.Address.Should().Be(deployment.Address);
        loaded.HealthStatus.Should().Be(HealthStatus.Healthy);
        loaded.LatencyMsP50.Should().Be(120);
        loaded.LastProbedAt.Should().Be(probedAt);
        loaded.LastProbedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
        loaded.ApiKeyEncrypted.Should().Be("CfDJ8-ciphertext");
    }

    [Fact]
    public async Task ModelVersionAndTrainingJob_WithJsonColumns_RoundTrip()
    {
        var tenant = TestData.Tenant();
        var model = TestData.ChatModel(tenant.Id);
        var dataset = Dataset.Create(tenant.Id, "faq", "file:///ml/data/faq.jsonl", "jsonl", 500, TestData.Now).Value;
        var job = TrainingJob.Create(tenant.Id, model.Id, dataset.Id, TrainingMethod.QLoRA,
            new Dictionary<string, string> { ["r"] = "16", ["lora_alpha"] = "32" }).Value;
        job.Start(TestData.Now, null);
        var version = ModelVersion.Create(tenant.Id, model.Id, "v1-qlora", "file:///ml/outputs/v1", job.Id,
            new Dictionary<string, double> { ["exact_match"] = 0.62 }, TestData.Now).Value;
        await using (var db = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(db, tenant, model, dataset, job, version);
        }

        await using var read = fixture.CreateDbContext();
        var loadedJob = await read.TrainingJobs.SingleAsync(j => j.Id == job.Id);
        var loadedVersion = await read.ModelVersions.SingleAsync(v => v.Id == version.Id);

        loadedJob.Status.Should().Be(TrainingJobStatus.Running);
        loadedJob.Hyperparameters.Should().Contain("lora_alpha", "32");
        loadedVersion.EvalMetrics.Should().Contain("exact_match", 0.62);
    }

    [Fact]
    public async Task UsageRecord_WithoutDeploymentOrTask_GetsDatabaseGeneratedId()
    {
        var tenant = TestData.Tenant();
        var consumer = TestData.Consumer(tenant.Id);
        var apiKey = TestData.ApiKey(tenant.Id, consumer.Id);
        var record = UsageRecord.Create(
            tenant.Id, apiKey.Id, deploymentId: null, "chat-general", task: null,
            TokenCount.Create(12, 0, isEstimated: true).Value, 30_000, 502, usedFallback: true, TestData.Now).Value;
        await using (var db = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(db, tenant, consumer, apiKey, record);
        }

        record.Id.Should().BePositive();
        await using var read = fixture.CreateDbContext();
        var loaded = await read.UsageRecords.SingleAsync(u => u.Id == record.Id);
        loaded.DeploymentId.Should().BeNull();
        loaded.Task.Should().BeNull();
        loaded.TokensEstimated.Should().BeTrue();
        loaded.StatusCode.Should().Be(502);
    }

    [Fact]
    public async Task Chunk_Embedding_RoundTripsAsVector1024()
    {
        var tenant = TestData.Tenant();
        var embeddingModel = TestData.EmbeddingModel(tenant.Id);
        var collection = TestData.Collection(tenant.Id, embeddingModel.Id);
        var document = TestData.Document(tenant.Id, collection.Id);
        var vector = TestData.UnitVector(7);
        vector[8] = 0.25f;
        var chunk = TestData.Chunk(tenant.Id, collection.Id, document.Id, 0, vector);
        await using (var db = fixture.CreateDbContext())
        {
            await TestData.SaveAsync(db, tenant, embeddingModel, collection, document, chunk);
        }

        await using var read = fixture.CreateDbContext();
        var loaded = await read.Chunks.SingleAsync(c => c.Id == chunk.Id);

        loaded.Embedding.ToArray().Should().Equal(vector);
    }
}
