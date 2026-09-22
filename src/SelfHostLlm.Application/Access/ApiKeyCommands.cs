using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Access;

public sealed record CreateApiKeyCommand(Guid TenantId, Guid ConsumerId, DateTimeOffset? ExpiresAt)
    : IRequest<Result<CreatedApiKey>>, ITenantRequest;

/// <summary>
/// Kết quả tạo key. <see cref="PlainText"/> chỉ tồn tại ở đây và chỉ trả cho client đúng một lần
/// (QĐ-9); entity lưu xuống DB chỉ có hash + prefix. <see cref="ToString"/> che plaintext.
/// </summary>
public sealed record CreatedApiKey(ApiKey ApiKey, string PlainText)
{
    public override string ToString() => $"{nameof(CreatedApiKey)} {{ Id = {ApiKey.Id}, Prefix = {ApiKey.KeyPrefix}, PlainText = *** }}";
}

/// <summary>Thu hồi key — không xoá cứng để giữ toàn vẹn usage lịch sử.</summary>
public sealed record RevokeApiKeyCommand(Guid TenantId, Guid ApiKeyId) : IRequest<Result<ApiKey>>, ITenantRequest;

public sealed record ListApiKeysQuery(Guid TenantId, Guid ConsumerId) : IRequest<Result<IReadOnlyList<ApiKey>>>, ITenantRequest;

internal sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(c => c.ConsumerId).NotEmpty();
    }
}

internal sealed class CreateApiKeyHandler(
    ITenantRepository<ApiKey> apiKeys,
    ITenantRepository<Consumer> consumers,
    ApiKeyHasher hasher,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<CreateApiKeyCommand, Result<CreatedApiKey>>
{
    public async Task<Result<CreatedApiKey>> HandleAsync(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        if (!await consumers.ExistsAsync(request.TenantId, c => c.Id == request.ConsumerId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<Consumer>(request.ConsumerId);
        }

        var generated = hasher.Generate();
        var created = ApiKey.Create(
            request.TenantId, request.ConsumerId, generated.Hash, generated.Prefix, request.ExpiresAt, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var apiKey = created.Value;
        apiKeys.Add(apiKey);
        audit.Record(request.TenantId, AuditAction.Create, nameof(ApiKey), apiKey.Id.ToString(), null, AuditSnapshot.Of(apiKey));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? new CreatedApiKey(apiKey, generated.PlainText) : saved.Error!;
    }
}

internal sealed class RevokeApiKeyHandler(ITenantRepository<ApiKey> apiKeys, IAuditTrail audit, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<RevokeApiKeyCommand, Result<ApiKey>>
{
    public async Task<Result<ApiKey>> HandleAsync(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        if (await apiKeys.GetAsync(request.TenantId, request.ApiKeyId, cancellationToken) is not { } apiKey)
        {
            return UseCaseExtensions.NotFound<ApiKey>(request.ApiKeyId);
        }

        var before = AuditSnapshot.Of(apiKey);
        var revoked = apiKey.Revoke(timeProvider.GetUtcNow());
        if (revoked.IsFailure)
        {
            return revoked.Error!;
        }

        audit.Record(request.TenantId, AuditAction.Revoke, nameof(ApiKey), apiKey.Id.ToString(), before, AuditSnapshot.Of(apiKey));
        return await unitOfWork.SaveAndReturnAsync(apiKey, cancellationToken);
    }
}

internal sealed class ListApiKeysHandler(ITenantRepository<ApiKey> apiKeys) : IRequestHandler<ListApiKeysQuery, Result<IReadOnlyList<ApiKey>>>
{
    public async Task<Result<IReadOnlyList<ApiKey>>> HandleAsync(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var list = await apiKeys.ListAsync(request.TenantId, k => k.ConsumerId == request.ConsumerId, cancellationToken);
        return Result<IReadOnlyList<ApiKey>>.Success(list.OrderBy(k => k.KeyPrefix, StringComparer.Ordinal).ToList());
    }
}
