using FluentValidation;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Usage;

namespace SelfHostLlm.Application.Usage;

public sealed record ListUsageRecordsQuery(
    Guid TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? ApiKeyId,
    Guid? DeploymentId,
    PageRequest Page) : IRequest<Result<PagedResult<UsageRecord>>>, ITenantRequest;

/// <summary>Tổng hợp usage trong [From, To), tuỳ chọn theo consumer.</summary>
public sealed record GetUsageSummaryQuery(Guid TenantId, Guid? ConsumerId, DateTimeOffset From, DateTimeOffset To)
    : IRequest<Result<UsageSummary>>, ITenantRequest;

internal static class ReportRules
{
    /// <summary>Giới hạn khoảng truy vấn tổng hợp để một request không quét cả bảng usage.</summary>
    public static readonly TimeSpan MaxSummaryRange = TimeSpan.FromDays(366);
}

internal sealed class ListUsageRecordsValidator : AbstractValidator<ListUsageRecordsQuery>
{
    public ListUsageRecordsValidator()
    {
        RuleFor(q => q.Page).ValidPage();
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From < q.To)
            .WithName("From").WithMessage("From phải trước To.");
    }
}

internal sealed class GetUsageSummaryValidator : AbstractValidator<GetUsageSummaryQuery>
{
    public GetUsageSummaryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("To phải sau From.");
        RuleFor(q => q).Must(q => q.To - q.From <= ReportRules.MaxSummaryRange)
            .WithName("To").WithMessage($"Khoảng thời gian tối đa {ReportRules.MaxSummaryRange.TotalDays} ngày.");
    }
}

internal sealed class ListUsageRecordsHandler(IUsageQueries usage)
    : IRequestHandler<ListUsageRecordsQuery, Result<PagedResult<UsageRecord>>>
{
    public async Task<Result<PagedResult<UsageRecord>>> HandleAsync(ListUsageRecordsQuery request, CancellationToken cancellationToken) =>
        Result<PagedResult<UsageRecord>>.Success(await usage.ListAsync(
            request.TenantId,
            new UsageRecordFilter(request.From, request.To, request.ApiKeyId, request.DeploymentId),
            request.Page,
            cancellationToken));
}

internal sealed class GetUsageSummaryHandler(IUsageQueries usage) : IRequestHandler<GetUsageSummaryQuery, Result<UsageSummary>>
{
    public async Task<Result<UsageSummary>> HandleAsync(GetUsageSummaryQuery request, CancellationToken cancellationToken) =>
        Result<UsageSummary>.Success(await usage.SummaryAsync(request.TenantId, request.ConsumerId, request.From, request.To, cancellationToken));
}
