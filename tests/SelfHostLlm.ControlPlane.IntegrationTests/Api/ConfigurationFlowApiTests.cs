using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SelfHostLlm.Application.Security;
using SelfHostLlm.ControlPlane.IntegrationTests.Infrastructure;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Contracts.Internal;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.ControlPlane.IntegrationTests.Api;

/// <summary>
/// Luồng cấu hình đầy đủ của một TenantAdmin: model → provider → deployment → virtual model →
/// route → consumer → api key → quota, rồi kiểm audit log và config snapshot cho Gateway.
/// </summary>
[Collection(PostgresDatabase.Name)]
public sealed class ConfigurationFlowApiTests(PostgresFixture fixture)
{
    private const string EngineKey = "engine-secret-9f8e7d";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private sealed record Configured(
        Guid TenantId, HttpClient Client, DeploymentResponse Deployment, ConsumerResponse Consumer, CreateApiKeyResponse ApiKey);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<Configured> ConfigureTenantAsync()
    {
        var app = await fixture.GetControlPlaneAsync();
        var (tenantId, client) = await app.NewTenantWithUserAsync(SystemRoles.TenantAdmin);
        var baseUrl = $"/api/v1/tenants/{tenantId}";

        var model = await ReadAsync<ModelResponse>(await client.PostAsJsonAsync($"{baseUrl}/models",
            new CreateModelRequest("Qwen2.5-7B", "qwen2.5", "7B", "Q4_K_M", 32768, ["Chat"], ["coding"])));
        var provider = await ReadAsync<ProviderResponse>(await client.PostAsJsonAsync($"{baseUrl}/providers",
            new CreateProviderRequest("Ollama PC", "Ollama", null)));
        var deployment = await ReadAsync<DeploymentResponse>(await client.PostAsJsonAsync($"{baseUrl}/deployments",
            new CreateDeploymentRequest(model.Id, null, provider.Id, "http://192.168.1.50:11434", "qwen2.5:7b-instruct-q4_K_M", EngineKey)));
        var virtualModel = await ReadAsync<VirtualModelResponse>(await client.PostAsJsonAsync($"{baseUrl}/virtual-models",
            new CreateVirtualModelRequest("code-fast", "Coding", null)));
        await ReadAsync<RouteResponse>(await client.PostAsJsonAsync($"{baseUrl}/virtual-models/{virtualModel.Id}/routes",
            new CreateRouteRequest(virtualModel.Id, deployment.Id, 0)));
        var consumer = await ReadAsync<ConsumerResponse>(await client.PostAsJsonAsync($"{baseUrl}/consumers",
            new CreateConsumerRequest("App nội bộ", null)));
        var apiKey = await ReadAsync<CreateApiKeyResponse>(await client.PostAsJsonAsync($"{baseUrl}/consumers/{consumer.Id}/api-keys",
            new CreateApiKeyRequest(consumer.Id, null)));
        await ReadAsync<QuotaResponse>(await client.PutAsJsonAsync($"{baseUrl}/consumers/{consumer.Id}/quota",
            new UpsertQuotaRequest(10_000, 1_000_000, 4)));

        return new Configured(tenantId, client, deployment, consumer, apiKey);
    }

    [Fact]
    public async Task FullFlow_ApiKeyPlaintextOnlyInCreateResponse()
    {
        var c = await ConfigureTenantAsync();

        c.ApiKey.Key.Should().StartWith("sk-");
        c.Deployment.HasApiKey.Should().BeTrue();

        var listed = await c.Client.GetStringAsync($"/api/v1/tenants/{c.TenantId}/consumers/{c.Consumer.Id}/api-keys");
        listed.Should().Contain(c.ApiKey.KeyPrefix).And.NotContain(c.ApiKey.Key);

        var deployment = await c.Client.GetStringAsync($"/api/v1/tenants/{c.TenantId}/deployments/{c.Deployment.Id}");
        deployment.Should().NotContain(EngineKey);
    }

    [Fact]
    public async Task FullFlow_EveryChangeIsAuditedWithoutSecrets()
    {
        var c = await ConfigureTenantAsync();

        var raw = await c.Client.GetStringAsync($"/api/v1/tenants/{c.TenantId}/audit-logs?pageSize=200");
        var entityTypes = JsonDocument.Parse(raw).RootElement.GetProperty("items").EnumerateArray()
            .Select(a => a.GetProperty("entityType").GetString()).ToList();

        entityTypes.Should().Contain(["Model", "Provider", "Deployment", "VirtualModel", "Route", "Consumer", "ApiKey", "Quota"]);
        raw.Should().NotContain(c.ApiKey.Key).And.NotContain(EngineKey).And.NotContain(ControlPlaneApp.AdminPassword);
    }

    [Fact]
    public async Task ConfigSnapshot_WithoutInternalToken_Returns401()
    {
        var app = await fixture.GetControlPlaneAsync();

        (await app.CreateClient().GetAsync("/internal/config-snapshot")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConfigSnapshot_ContainsHashAndCiphertextButNoPlaintext()
    {
        var c = await ConfigureTenantAsync();
        var app = await fixture.GetControlPlaneAsync();
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(ConfigSnapshotDto.InternalTokenHeader, ControlPlaneApp.InternalToken);

        var response = await client.GetAsync("/internal/config-snapshot");
        var raw = await response.Content.ReadAsStringAsync();
        var snapshot = JsonSerializer.Deserialize<ConfigSnapshotDto>(raw, WebJson)!;

        response.Headers.ETag.Should().NotBeNull();
        raw.Should().NotContain(c.ApiKey.Key).And.NotContain(EngineKey);
        snapshot.ApiKeys.Should().Contain(k => k.Id == c.ApiKey.Id && k.KeyHash == new ApiKeyHasher().ComputeHash(c.ApiKey.Key));
        var deployment = snapshot.Deployments.Single(d => d.Id == c.Deployment.Id);
        deployment.ApiKeyEncrypted.Should().NotBeNullOrEmpty().And.NotBe(EngineKey);
        deployment.ProviderKind.Should().Be("Ollama");
        snapshot.Routes.Should().Contain(r => r.DeploymentId == c.Deployment.Id && r.TenantId == c.TenantId);
        snapshot.Quotas.Should().Contain(q => q.ConsumerId == c.Consumer.Id && q.TokensPerMinute == 10_000);
    }

    [Fact]
    public async Task ConfigSnapshot_SameContent_Returns304ForMatchingETag()
    {
        var app = await fixture.GetControlPlaneAsync();
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(ConfigSnapshotDto.InternalTokenHeader, ControlPlaneApp.InternalToken);
        var first = await client.GetAsync("/internal/config-snapshot");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/config-snapshot");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(request);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task ConfigSnapshot_RevokedKey_IsExcluded()
    {
        var c = await ConfigureTenantAsync();
        (await c.Client.PostAsync($"/api/v1/tenants/{c.TenantId}/api-keys/{c.ApiKey.Id}/revoke", null)).EnsureSuccessStatusCode();
        var app = await fixture.GetControlPlaneAsync();
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(ConfigSnapshotDto.InternalTokenHeader, ControlPlaneApp.InternalToken);

        var snapshot = await client.GetFromJsonAsync<ConfigSnapshotDto>("/internal/config-snapshot");

        snapshot!.ApiKeys.Should().NotContain(k => k.Id == c.ApiKey.Id);
    }
}
