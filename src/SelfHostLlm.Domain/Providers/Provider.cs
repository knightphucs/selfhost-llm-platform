using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Providers;

/// <summary>Một backend suy luận đã đăng ký (ví dụ "Ollama trên PC").</summary>
public sealed class Provider : Entity<Guid>, ITenantScoped
{
    private Provider()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public ProviderKind Kind { get; private set; }

    public string? Description { get; private set; }

    public static Result<Provider> Create(Guid tenantId, string name, ProviderKind kind, string? description)
    {
        var provider = new Provider { Id = Guid.NewGuid(), TenantId = tenantId };
        var error = Guard.NotEmpty(tenantId, "provider.tenant_id") ?? provider.Apply(name, kind, description);
        return error is null ? provider : error;
    }

    public Result Update(string name, ProviderKind kind, string? description)
    {
        var error = Apply(name, kind, description);
        return error is null ? Result.Success() : error;
    }

    private Error? Apply(string name, ProviderKind kind, string? description)
    {
        var error = Guard.First(
            Guard.NotBlank(name, "provider.name"),
            Enum.IsDefined(kind) ? null : Error.Validation("provider.kind.invalid", "ProviderKind không hợp lệ."));
        if (error is not null)
        {
            return error;
        }

        Name = name.Trim();
        Kind = kind;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return null;
    }
}
