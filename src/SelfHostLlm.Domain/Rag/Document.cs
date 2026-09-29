using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Rag;

/// <summary>
/// Tài liệu đã ingest vào một <see cref="Collection"/>. <c>tenant_id</c> lặp lại có chủ đích
/// (phi chuẩn hoá) để mọi truy vấn lọc tenant trực tiếp trong WHERE.
/// </summary>
public sealed class Document : Entity<Guid>, ITenantScoped
{
    public const int ContentHashLength = 64;

    private Document()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CollectionId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? SourceUri { get; private set; }

    /// <summary>SHA-256 hex của nội dung — chống ingest trùng cùng một tài liệu.</summary>
    public string ContentHash { get; private set; } = null!;

    public string MimeType { get; private set; } = null!;

    public DateTimeOffset IngestedAt { get; private set; }

    public static Result<Document> Create(
        Guid tenantId,
        Guid collectionId,
        string title,
        string? sourceUri,
        string contentHash,
        string mimeType,
        DateTimeOffset now)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "document.tenant_id"),
            Guard.NotEmpty(collectionId, "document.collection_id"),
            Guard.NotBlank(title, "document.title", 500),
            Hex.IsLowerHex(contentHash, ContentHashLength)
                ? null
                : Error.Validation("document.content_hash.invalid", "content_hash phải là SHA-256 hex."),
            Guard.NotBlank(mimeType, "document.mime_type", 100));
        if (error is not null)
        {
            return error;
        }

        return new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CollectionId = collectionId,
            Title = title.Trim(),
            SourceUri = string.IsNullOrWhiteSpace(sourceUri) ? null : sourceUri.Trim(),
            ContentHash = contentHash,
            MimeType = mimeType.Trim(),
            IngestedAt = now.ToUniversalTime(),
        };
    }
}
