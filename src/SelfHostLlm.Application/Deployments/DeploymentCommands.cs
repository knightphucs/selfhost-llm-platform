using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Deployments;
using SelfHostLlm.Domain.Models;
using SelfHostLlm.Domain.Providers;

namespace SelfHostLlm.Application.Deployments;

/// <param name="ApiKey">API key phía engine (nếu có) — được mã hoá bằng <see cref="ISecretProtector"/> trước khi lưu.</param>
public sealed record CreateDeploymentCommand(
    Guid TenantId,
    Guid ModelId,
    Guid? ModelVersionId,
    Guid ProviderId,
    string BaseUrl,
    string RemoteModelName,
    string? ApiKey) : IRequest<Result<Deployment>>, ITenantRequest
{
    public override string ToString() =>
        $"{nameof(CreateDeploymentCommand)} {{ TenantId = {TenantId}, ModelId = {ModelId}, ProviderId = {ProviderId}, " +
        $"BaseUrl = {BaseUrl}, RemoteModelName = {RemoteModelName}, ApiKey = {(ApiKey is null ? "null" : "***")} }}";
}

public sealed record UpdateDeploymentEndpointCommand(Guid TenantId, Guid DeploymentId, string BaseUrl, string RemoteModelName)
    : IRequest<Result<Deployment>>, ITenantRequest;

/// <param name="ApiKey">Key mới; <c>null</c> hoặc rỗng để xoá key.</param>
public sealed record SetDeploymentApiKeyCommand(Guid TenantId, Guid DeploymentId, string? ApiKey)
    : IRequest<Result<Deployment>>, ITenantRequest
{
    public override string ToString() =>
        $"{nameof(SetDeploymentApiKeyCommand)} {{ TenantId = {TenantId}, DeploymentId = {DeploymentId}, ApiKey = {(ApiKey is null ? "null" : "***")} }}";
}

/// <summary>Bật/tắt deployment — thay cho xoá cứng (giữ toàn vẹn usage lịch sử).</summary>
public sealed record SetDeploymentEnabledCommand(Guid TenantId, Guid DeploymentId, bool Enabled)
    : IRequest<Result<Deployment>>, ITenantRequest;

internal static class DeploymentRules
{
    public static IRuleBuilderOptions<T, string> BaseUrl<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(500)
            .Must(url => Address.Create(url).IsSuccess)
            .WithMessage("base_url phải là URL tuyệt đối http/https, không có query.");
}

internal sealed class CreateDeploymentValidator : AbstractValidator<CreateDeploymentCommand>
{
    public CreateDeploymentValidator()
    {
        RuleFor(c => c.ModelId).NotEmpty();
        RuleFor(c => c.ProviderId).NotEmpty();
        RuleFor(c => c.BaseUrl).BaseUrl();
        RuleFor(c => c.RemoteModelName).Name();
        RuleFor(c => c.ApiKey).MaximumLength(4096);
    }
}

internal sealed class UpdateDeploymentEndpointValidator : AbstractValidator<UpdateDeploymentEndpointCommand>
{
    public UpdateDeploymentEndpointValidator()
    {
        RuleFor(c => c.DeploymentId).NotEmpty();
        RuleFor(c => c.BaseUrl).BaseUrl();
        RuleFor(c => c.RemoteModelName).Name();
    }
}

internal sealed class SetDeploymentApiKeyValidator : AbstractValidator<SetDeploymentApiKeyCommand>
{
    public SetDeploymentApiKeyValidator()
    {
        RuleFor(c => c.DeploymentId).NotEmpty();
        RuleFor(c => c.ApiKey).MaximumLength(4096);
    }
}

internal sealed class CreateDeploymentHandler(
    ITenantRepository<Deployment> deployments,
    ITenantRepository<Model> models,
    ITenantRepository<ModelVersion> modelVersions,
    ITenantRepository<Provider> providers,
    ISecretProtector secrets,
    IAuditTrail audit,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateDeploymentCommand, Result<Deployment>>
{
    public async Task<Result<Deployment>> HandleAsync(CreateDeploymentCommand request, CancellationToken cancellationToken)
    {
        // Mọi tham chiếu phải thuộc cùng tenant — composite FK ở DB là lớp chặn thứ hai.
        if (!await models.ExistsAsync(request.TenantId, m => m.Id == request.ModelId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<Model>(request.ModelId);
        }

        if (!await providers.ExistsAsync(request.TenantId, p => p.Id == request.ProviderId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<Provider>(request.ProviderId);
        }

        if (request.ModelVersionId is { } versionId
            && !await modelVersions.ExistsAsync(request.TenantId, v => v.Id == versionId && v.ModelId == request.ModelId, cancellationToken))
        {
            return UseCaseExtensions.NotFound<ModelVersion>(versionId);
        }

        var address = Address.Create(request.BaseUrl);
        if (address.IsFailure)
        {
            return address.Error!;
        }

        var remoteName = request.RemoteModelName.Trim();
        if (await deployments.ExistsAsync(
                request.TenantId,
                d => d.ProviderId == request.ProviderId && d.Address == address.Value && d.RemoteModelName == remoteName,
                cancellationToken))
        {
            return Error.Conflict("deployment.duplicate", "Đã có deployment cùng provider, address và remote model.");
        }

        var created = Deployment.Create(
            request.TenantId, request.ModelId, request.ModelVersionId, request.ProviderId, address.Value, remoteName,
            string.IsNullOrEmpty(request.ApiKey) ? null : secrets.Protect(request.ApiKey));
        if (created.IsFailure)
        {
            return created;
        }

        var deployment = created.Value;
        deployments.Add(deployment);
        audit.Record(request.TenantId, AuditAction.Create, nameof(Deployment), deployment.Id.ToString(), null, AuditSnapshot.Of(deployment));
        return await unitOfWork.SaveAndReturnAsync(deployment, cancellationToken);
    }
}

internal sealed class UpdateDeploymentEndpointHandler(ITenantRepository<Deployment> deployments, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDeploymentEndpointCommand, Result<Deployment>>
{
    public async Task<Result<Deployment>> HandleAsync(UpdateDeploymentEndpointCommand request, CancellationToken cancellationToken)
    {
        if (await deployments.GetAsync(request.TenantId, request.DeploymentId, cancellationToken) is not { } deployment)
        {
            return UseCaseExtensions.NotFound<Deployment>(request.DeploymentId);
        }

        var address = Address.Create(request.BaseUrl);
        if (address.IsFailure)
        {
            return address.Error!;
        }

        var before = AuditSnapshot.Of(deployment);
        var updated = deployment.UpdateEndpoint(address.Value, request.RemoteModelName);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(Deployment), deployment.Id.ToString(), before, AuditSnapshot.Of(deployment));
        return await unitOfWork.SaveAndReturnAsync(deployment, cancellationToken);
    }
}

internal sealed class SetDeploymentApiKeyHandler(
    ITenantRepository<Deployment> deployments,
    ISecretProtector secrets,
    IAuditTrail audit,
    IUnitOfWork unitOfWork) : IRequestHandler<SetDeploymentApiKeyCommand, Result<Deployment>>
{
    public async Task<Result<Deployment>> HandleAsync(SetDeploymentApiKeyCommand request, CancellationToken cancellationToken)
    {
        if (await deployments.GetAsync(request.TenantId, request.DeploymentId, cancellationToken) is not { } deployment)
        {
            return UseCaseExtensions.NotFound<Deployment>(request.DeploymentId);
        }

        var before = AuditSnapshot.Of(deployment);
        deployment.SetApiKeyEncrypted(string.IsNullOrEmpty(request.ApiKey) ? null : secrets.Protect(request.ApiKey));

        // Snapshot chỉ ghi hasApiKey — đổi key sang key khác vẫn để lại vết (có thao tác Update) nhưng không lộ giá trị.
        audit.Record(request.TenantId, AuditAction.Update, nameof(Deployment), deployment.Id.ToString(), before, AuditSnapshot.Of(deployment));
        return await unitOfWork.SaveAndReturnAsync(deployment, cancellationToken);
    }
}

internal sealed class SetDeploymentEnabledHandler(ITenantRepository<Deployment> deployments, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<SetDeploymentEnabledCommand, Result<Deployment>>
{
    public async Task<Result<Deployment>> HandleAsync(SetDeploymentEnabledCommand request, CancellationToken cancellationToken)
    {
        if (await deployments.GetAsync(request.TenantId, request.DeploymentId, cancellationToken) is not { } deployment)
        {
            return UseCaseExtensions.NotFound<Deployment>(request.DeploymentId);
        }

        if (deployment.Enabled == request.Enabled)
        {
            return deployment;
        }

        var before = AuditSnapshot.Of(deployment);
        if (request.Enabled)
        {
            deployment.Enable();
        }
        else
        {
            deployment.Disable();
        }

        audit.Record(
            request.TenantId,
            request.Enabled ? AuditAction.Enable : AuditAction.Disable,
            nameof(Deployment),
            deployment.Id.ToString(),
            before,
            AuditSnapshot.Of(deployment));
        return await unitOfWork.SaveAndReturnAsync(deployment, cancellationToken);
    }
}
