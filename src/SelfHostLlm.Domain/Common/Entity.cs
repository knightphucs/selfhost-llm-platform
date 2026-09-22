namespace SelfHostLlm.Domain.Common;

/// <summary>Base cho entity có định danh. Id do factory gán (Guid) hoặc database sinh (long).</summary>
public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;
}
