using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Access;

/// <summary>
/// API key của một <see cref="Consumer"/>. Chỉ lưu SHA-256 hash và prefix hiển thị —
/// plaintext chỉ tồn tại đúng một lần lúc tạo, ở tầng Application (QĐ-9).
/// Không xoá cứng: thu hồi bằng <see cref="Revoke"/>.
/// </summary>
public sealed class ApiKey : Entity<Guid>, ITenantScoped
{
    /// <summary>Độ dài hex của SHA-256.</summary>
    public const int KeyHashLength = 64;

    public const int MaxKeyPrefixLength = 16;

    private ApiKey()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ConsumerId { get; private set; }

    /// <summary>SHA-256 dạng hex thường của key gốc.</summary>
    public string KeyHash { get; private set; } = null!;

    /// <summary>Vài ký tự đầu của key (ví dụ <c>sk-a1b2</c>) để người dùng nhận ra key.</summary>
    public string KeyPrefix { get; private set; } = null!;

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static Result<ApiKey> Create(
        Guid tenantId,
        Guid consumerId,
        string keyHash,
        string keyPrefix,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        var error = Guard.First(
            Guard.NotEmpty(tenantId, "api_key.tenant_id"),
            Guard.NotEmpty(consumerId, "api_key.consumer_id"),
            IsLowerHex(keyHash, KeyHashLength)
                ? null
                : Error.Validation("api_key.key_hash.invalid", "key_hash phải là SHA-256 hex (64 ký tự thường)."),
            Guard.NotBlank(keyPrefix, "api_key.key_prefix", MaxKeyPrefixLength),
            expiresAt is { } exp && exp <= now
                ? Error.Validation("api_key.expires_at.past", "expires_at phải ở tương lai.")
                : null);
        if (error is not null)
        {
            return error;
        }

        return new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConsumerId = consumerId,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            ExpiresAt = expiresAt?.ToUniversalTime(),
        };
    }

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Key dùng được tại thời điểm <paramref name="now"/>: chưa thu hồi và chưa hết hạn.</summary>
    public bool IsActiveAt(DateTimeOffset now) => !IsRevoked && (ExpiresAt is null || ExpiresAt > now);

    public Result Revoke(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return Error.Conflict("api_key.already_revoked", "API key đã bị thu hồi trước đó.");
        }

        RevokedAt = now.ToUniversalTime();
        return Result.Success();
    }

    public void MarkUsed(DateTimeOffset now) => LastUsedAt = now.ToUniversalTime();

    private static bool IsLowerHex(string? value, int length) =>
        value is not null && value.Length == length && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
