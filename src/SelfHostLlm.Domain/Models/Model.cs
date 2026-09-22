using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Models;

/// <summary>
/// Model logic (ví dụ <c>Qwen2.5-7B</c>). Một model có thể chạy ở nhiều <c>Deployment</c>;
/// gateway coi các deployment đó là endpoint thay thế nhau.
/// </summary>
public sealed class Model : Entity<Guid>, ITenantScoped
{
    private List<ModelCapability> _capabilities = [];
    private List<string> _taskTags = [];

    private Model()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public string Family { get; private set; } = null!;

    /// <summary>Kích thước tham số dạng hiển thị, ví dụ <c>7B</c>.</summary>
    public string ParamSize { get; private set; } = null!;

    /// <summary>Ví dụ <c>Q4_K_M</c>, <c>AWQ</c>, <c>FP16</c>.</summary>
    public string Quantization { get; private set; } = null!;

    public int ContextLength { get; private set; }

    public IReadOnlyList<ModelCapability> Capabilities => _capabilities;

    /// <summary>Nhãn đầu việc tự do (<c>coding</c>, <c>chat</c>...) dùng khi gợi ý routing.</summary>
    public IReadOnlyList<string> TaskTags => _taskTags;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Model> Create(
        Guid tenantId,
        string name,
        string family,
        string paramSize,
        string quantization,
        int contextLength,
        IEnumerable<ModelCapability> capabilities,
        IEnumerable<string> taskTags,
        DateTimeOffset now)
    {
        var model = new Model
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = now.ToUniversalTime(),
        };

        var error = Guard.NotEmpty(tenantId, "model.tenant_id")
            ?? model.Apply(name, family, paramSize, quantization, contextLength, capabilities, taskTags);
        return error is null ? model : error;
    }

    public Result Update(
        string name,
        string family,
        string paramSize,
        string quantization,
        int contextLength,
        IEnumerable<ModelCapability> capabilities,
        IEnumerable<string> taskTags)
    {
        var error = Apply(name, family, paramSize, quantization, contextLength, capabilities, taskTags);
        return error is null ? Result.Success() : error;
    }

    public bool Supports(ModelCapability capability) => _capabilities.Contains(capability);

    private Error? Apply(
        string name,
        string family,
        string paramSize,
        string quantization,
        int contextLength,
        IEnumerable<ModelCapability> capabilities,
        IEnumerable<string> taskTags)
    {
        var capabilityList = capabilities?.Distinct().ToList() ?? [];
        var tagList = taskTags?
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? [];

        var error = Guard.First(
            Guard.NotBlank(name, "model.name"),
            Guard.NotBlank(family, "model.family", 100),
            Guard.NotBlank(paramSize, "model.param_size", 20),
            Guard.NotBlank(quantization, "model.quantization", 50),
            Guard.Positive(contextLength, "model.context_length"),
            capabilityList.Count == 0
                ? Error.Validation("model.capabilities.empty", "Model phải có ít nhất một capability.")
                : null);
        if (error is not null)
        {
            return error;
        }

        Name = name.Trim();
        Family = family.Trim();
        ParamSize = paramSize.Trim();
        Quantization = quantization.Trim();
        ContextLength = contextLength;
        _capabilities = capabilityList;
        _taskTags = tagList;
        return null;
    }
}
