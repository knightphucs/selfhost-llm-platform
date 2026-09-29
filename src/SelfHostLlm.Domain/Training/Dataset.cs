using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Training;

/// <summary>Dataset dùng để fine-tune. Dữ liệu nằm ở <see cref="StorageUri"/>, không nằm trong DB.</summary>
public sealed class Dataset : Entity<Guid>, ITenantScoped
{
    private Dataset()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public string StorageUri { get; private set; } = null!;

    /// <summary>Ví dụ <c>jsonl</c>, <c>alpaca</c>, <c>sharegpt</c>.</summary>
    public string Format { get; private set; } = null!;

    public int RecordCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Dataset> Create(
        Guid tenantId,
        string name,
        string storageUri,
        string format,
        int recordCount,
        DateTimeOffset now)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "dataset.tenant_id"),
            Guard.NotBlank(name, "dataset.name"),
            Guard.NotBlank(storageUri, "dataset.storage_uri", 1000),
            Guard.NotBlank(format, "dataset.format", 50),
            Guard.NonNegative(recordCount, "dataset.record_count"));
        if (error is not null)
        {
            return error;
        }

        return new Dataset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            StorageUri = storageUri.Trim(),
            Format = format.Trim().ToLowerInvariant(),
            RecordCount = recordCount,
            CreatedAt = now.ToUniversalTime(),
        };
    }
}
