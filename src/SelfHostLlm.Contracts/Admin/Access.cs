using SelfHostLlm.Contracts.Admin.Common;

namespace SelfHostLlm.Contracts.Admin;

public sealed record CreateConsumerRequest(string Name, string? Description);

public sealed record UpdateConsumerRequest(string Name, string? Description, bool Enabled);

public sealed record ConsumerResponse(Guid Id, string Name, string? Description, bool Enabled);

public sealed record CreateApiKeyRequest(Guid ConsumerId, DateTimeOffset? ExpiresAt);

/// <summary>
/// Response duy nhất chứa key plaintext — chỉ trả đúng một lần lúc tạo (QĐ-9).
/// <see cref="ToString"/> che key để không lọt vào log.
/// </summary>
public sealed record CreateApiKeyResponse(Guid Id, Guid ConsumerId, string Key, string KeyPrefix, DateTimeOffset? ExpiresAt)
{
    public override string ToString() =>
        $"{nameof(CreateApiKeyResponse)} {{ Id = {Id}, ConsumerId = {ConsumerId}, Key = {Secret.Masked}, " +
        $"KeyPrefix = {KeyPrefix}, ExpiresAt = {ExpiresAt} }}";
}

/// <summary>Thông tin key khi liệt kê — không có key plaintext hay hash.</summary>
public sealed record ApiKeyResponse(
    Guid Id,
    Guid ConsumerId,
    string KeyPrefix,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    bool IsActive);

/// <summary>Đặt quota cho consumer (<c>PUT</c>, tạo nếu chưa có). <c>null</c> = không giới hạn.</summary>
public sealed record UpsertQuotaRequest(int? TokensPerMinute, long? TokensPerMonth, int? MaxConcurrentRequests);

public sealed record QuotaResponse(
    Guid Id,
    Guid ConsumerId,
    int? TokensPerMinute,
    long? TokensPerMonth,
    int? MaxConcurrentRequests);
