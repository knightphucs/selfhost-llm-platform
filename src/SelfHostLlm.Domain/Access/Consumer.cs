using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Access;

/// <summary>Chủ thể gọi API suy luận (một ứng dụng, một nhóm). Sở hữu ApiKey và gắn Quota.</summary>
public sealed class Consumer : Entity<Guid>, ITenantScoped
{
    private Consumer()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool Enabled { get; private set; }

    public static Result<Consumer> Create(Guid tenantId, string name, string? description)
    {
        var consumer = new Consumer { Id = Guid.NewGuid(), TenantId = tenantId, Enabled = true };
        var error = Guard.NotEmpty(tenantId, "consumer.tenant_id") ?? consumer.Apply(name, description);
        return error is null ? consumer : error;
    }

    public Result Update(string name, string? description)
    {
        var error = Apply(name, description);
        return error is null ? Result.Success() : error;
    }

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    private Error? Apply(string name, string? description)
    {
        var error = Guard.NotBlank(name, "consumer.name");
        if (error is not null)
        {
            return error;
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return null;
    }
}
