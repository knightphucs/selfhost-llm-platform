using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;
using SelfHostLlm.Domain.Routing;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.Auditing;

/// <summary>
/// Snapshot ghi vào <c>before_value</c>/<c>after_value</c>. Cố ý chỉ chọn field an toàn:
/// KHÔNG có key hash, ciphertext của API key engine, hay plaintext nào.
/// </summary>
public static class AuditSnapshot
{
    public static TenantSnapshot Of(Tenant t) => new(t.Id, t.Name, t.Slug);

    public static ModelSnapshot Of(Model m) => new(
        m.Id, m.Name, m.Family, m.ParamSize, m.Quantization, m.ContextLength,
        m.Capabilities.Select(c => c.ToString()).ToArray(), m.TaskTags.ToArray());

    public static ProviderSnapshot Of(Provider p) => new(p.Id, p.Name, p.Kind.ToString(), p.Description);

    public static DeploymentSnapshot Of(Deployment d) => new(
        d.Id, d.ModelId, d.ModelVersionId, d.ProviderId, d.Address.ToString(), d.RemoteModelName,
        HasApiKey: d.ApiKeyEncrypted is not null, d.Enabled);

    public static VirtualModelSnapshot Of(VirtualModel v) => new(v.Id, v.Name, v.Task.ToString(), v.Description);

    public static RouteSnapshot Of(Route r) => new(r.Id, r.VirtualModelId, r.DeploymentId, r.Priority, r.Weight, r.Enabled);

    public static ConsumerSnapshot Of(Consumer c) => new(c.Id, c.Name, c.Description, c.Enabled);

    public static ApiKeySnapshot Of(ApiKey k) => new(k.Id, k.ConsumerId, k.KeyPrefix, k.ExpiresAt, k.RevokedAt);

    public static UserSnapshot Of(UserAccount u) => new(u.Id, u.Username, u.Email, u.Enabled, u.Roles.ToArray());

    public static QuotaSnapshot Of(Quota q) => new(q.Id, q.ConsumerId, q.TokensPerMinute, q.TokensPerMonth, q.MaxConcurrentRequests);
}

public sealed record TenantSnapshot(Guid Id, string Name, string Slug);

public sealed record ModelSnapshot(
    Guid Id,
    string Name,
    string Family,
    string ParamSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TaskTags);

public sealed record ProviderSnapshot(Guid Id, string Name, string Kind, string? Description);

/// <summary>Chỉ ghi có/không có API key engine — không bao giờ ghi ciphertext.</summary>
public sealed record DeploymentSnapshot(
    Guid Id,
    Guid ModelId,
    Guid? ModelVersionId,
    Guid ProviderId,
    string BaseUrl,
    string RemoteModelName,
    bool HasApiKey,
    bool Enabled);

public sealed record VirtualModelSnapshot(Guid Id, string Name, string Task, string? Description);

public sealed record RouteSnapshot(Guid Id, Guid VirtualModelId, Guid DeploymentId, int Priority, int Weight, bool Enabled);

public sealed record ConsumerSnapshot(Guid Id, string Name, string? Description, bool Enabled);

/// <summary>Chỉ prefix hiển thị — không có key hash.</summary>
public sealed record ApiKeySnapshot(Guid Id, Guid ConsumerId, string KeyPrefix, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);

public sealed record QuotaSnapshot(Guid Id, Guid ConsumerId, int? TokensPerMinute, long? TokensPerMonth, int? MaxConcurrentRequests);

/// <summary>Không có password hash hay security stamp.</summary>
public sealed record UserSnapshot(Guid Id, string Username, string? Email, bool Enabled, IReadOnlyList<string> Roles);
