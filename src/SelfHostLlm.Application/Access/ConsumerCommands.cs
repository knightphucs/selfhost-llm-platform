using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Access;

public sealed record CreateConsumerCommand(Guid TenantId, string Name, string? Description)
    : IRequest<Result<Consumer>>, ITenantRequest;

/// <summary>Sửa consumer. Không có xoá — consumer gắn với ApiKey và usage lịch sử; tắt bằng <c>Enabled = false</c>.</summary>
public sealed record UpdateConsumerCommand(Guid TenantId, Guid ConsumerId, string Name, string? Description, bool Enabled)
    : IRequest<Result<Consumer>>, ITenantRequest;

public sealed record GetConsumerQuery(Guid TenantId, Guid ConsumerId) : IRequest<Result<Consumer>>, ITenantRequest;

public sealed record ListConsumersQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<Consumer>>>, ITenantRequest;

internal sealed class CreateConsumerValidator : AbstractValidator<CreateConsumerCommand>
{
    public CreateConsumerValidator()
    {
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

internal sealed class UpdateConsumerValidator : AbstractValidator<UpdateConsumerCommand>
{
    public UpdateConsumerValidator()
    {
        RuleFor(c => c.ConsumerId).NotEmpty();
        RuleFor(c => c.Name).Name();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}

internal sealed class CreateConsumerHandler(ITenantRepository<Consumer> consumers, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateConsumerCommand, Result<Consumer>>
{
    public async Task<Result<Consumer>> HandleAsync(CreateConsumerCommand request, CancellationToken cancellationToken)
    {
        var created = Consumer.Create(request.TenantId, request.Name, request.Description);
        if (created.IsFailure)
        {
            return created;
        }

        var consumer = created.Value;
        consumers.Add(consumer);
        audit.Record(request.TenantId, AuditAction.Create, nameof(Consumer), consumer.Id.ToString(), null, AuditSnapshot.Of(consumer));
        return await unitOfWork.SaveAndReturnAsync(consumer, cancellationToken);
    }
}

internal sealed class UpdateConsumerHandler(ITenantRepository<Consumer> consumers, IAuditTrail audit, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateConsumerCommand, Result<Consumer>>
{
    public async Task<Result<Consumer>> HandleAsync(UpdateConsumerCommand request, CancellationToken cancellationToken)
    {
        if (await consumers.GetAsync(request.TenantId, request.ConsumerId, cancellationToken) is not { } consumer)
        {
            return UseCaseExtensions.NotFound<Consumer>(request.ConsumerId);
        }

        var before = AuditSnapshot.Of(consumer);
        var updated = consumer.Update(request.Name, request.Description);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        if (request.Enabled)
        {
            consumer.Enable();
        }
        else
        {
            consumer.Disable();
        }

        audit.Record(request.TenantId, AuditAction.Update, nameof(Consumer), consumer.Id.ToString(), before, AuditSnapshot.Of(consumer));
        return await unitOfWork.SaveAndReturnAsync(consumer, cancellationToken);
    }
}

internal sealed class GetConsumerHandler(ITenantRepository<Consumer> consumers) : IRequestHandler<GetConsumerQuery, Result<Consumer>>
{
    public async Task<Result<Consumer>> HandleAsync(GetConsumerQuery request, CancellationToken cancellationToken) =>
        await consumers.GetAsync(request.TenantId, request.ConsumerId, cancellationToken) is { } consumer
            ? consumer
            : UseCaseExtensions.NotFound<Consumer>(request.ConsumerId);
}

internal sealed class ListConsumersHandler(ITenantRepository<Consumer> consumers)
    : IRequestHandler<ListConsumersQuery, Result<IReadOnlyList<Consumer>>>
{
    public async Task<Result<IReadOnlyList<Consumer>>> HandleAsync(ListConsumersQuery request, CancellationToken cancellationToken)
    {
        var list = await consumers.ListAsync(request.TenantId, null, cancellationToken);
        return Result<IReadOnlyList<Consumer>>.Success(list.OrderBy(c => c.Name, StringComparer.Ordinal).ToList());
    }
}
