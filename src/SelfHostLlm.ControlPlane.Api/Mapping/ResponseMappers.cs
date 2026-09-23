using System.Text.Json;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Access;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Contracts.Admin;
using SelfHostLlm.Contracts.Admin.Common;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Domain.Tenancy;
using SelfHostLlm.Domain.Usage;
using Route = SelfHostLlm.Domain.Routing.Route;

namespace SelfHostLlm.ControlPlane.Api.Mapping;

/// <summary>Entity Domain → DTO Contracts. Không bao giờ map key hash, ciphertext hay plaintext (trừ lúc tạo key).</summary>
internal static class ResponseMappers
{
    public static TenantResponse ToResponse(this Tenant t) => new(t.Id, t.Name, t.Slug, t.CreatedAt);

    public static ModelResponse ToResponse(this Model m) => new(
        m.Id, m.Name, m.Family, m.ParamSize, m.Quantization, m.ContextLength,
        m.Capabilities.Select(c => c.ToString()).ToList(), m.TaskTags.ToList(), m.CreatedAt);

    public static ProviderResponse ToResponse(this Provider p) => new(p.Id, p.Name, p.Kind.ToString(), p.Description);

    public static DeploymentResponse ToResponse(this Deployment d) => new(
        d.Id, d.ModelId, d.ModelVersionId, d.ProviderId, d.Address.ToString(), d.RemoteModelName,
        HasApiKey: d.ApiKeyEncrypted is not null, d.HealthStatus.ToString(), d.ConsecutiveFailures, d.LatencyMsP50,
        d.LastProbedAt, d.Enabled);

    public static VirtualModelResponse ToResponse(this VirtualModel v) => new(v.Id, v.Name, v.Task.ToString(), v.Description);

    public static RouteResponse ToResponse(this Route r) => new(r.Id, r.VirtualModelId, r.DeploymentId, r.Priority, r.Weight, r.Enabled);

    public static ConsumerResponse ToResponse(this Consumer c) => new(c.Id, c.Name, c.Description, c.Enabled);

    public static ApiKeyResponse ToResponse(this ApiKey k, DateTimeOffset now) =>
        new(k.Id, k.ConsumerId, k.KeyPrefix, k.ExpiresAt, k.LastUsedAt, k.RevokedAt, k.IsActiveAt(now));

    /// <summary>Response DUY NHẤT chứa key plaintext.</summary>
    public static CreateApiKeyResponse ToResponse(this CreatedApiKey created) =>
        new(created.ApiKey.Id, created.ApiKey.ConsumerId, created.PlainText, created.ApiKey.KeyPrefix, created.ApiKey.ExpiresAt);

    public static QuotaResponse ToResponse(this Quota q) =>
        new(q.Id, q.ConsumerId, q.TokensPerMinute, q.TokensPerMonth, q.MaxConcurrentRequests);

    public static UserResponse ToResponse(this UserAccount u) => new(u.Id, u.Username, u.Email, u.Enabled, u.Roles);

    public static UsageRecordResponse ToResponse(this UsageRecord u) => new(
        u.Id, u.ApiKeyId, u.DeploymentId, u.RequestedModel, u.Task?.ToString(), u.PromptTokens, u.CompletionTokens,
        u.TokensEstimated, u.LatencyMs, u.StatusCode, u.UsedFallback, u.OccurredAt);

    public static UsageSummaryResponse ToResponse(this UsageSummary s) => new(
        s.ConsumerId, s.From, s.To, s.PromptTokens, s.CompletionTokens, s.TotalTokens, s.RequestCount,
        s.EstimatedRequestCount, s.FallbackRequestCount);

    public static AuditLogResponse ToResponse(this AuditLog a) => new(
        a.Id, a.ActorUserId, a.Action.ToString(), a.EntityType, a.EntityId, ParseJson(a.BeforeValue), ParseJson(a.AfterValue),
        a.IpAddress, a.OccurredAt);

    public static PagedResponse<TResponse> ToResponse<T, TResponse>(this PagedResult<T> page, Func<T, TResponse> map) =>
        new(page.Items.Select(map).ToList(), page.Page, page.PageSize, page.Total);

    private static JsonElement? ParseJson(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
