using FluentValidation;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Common.Behaviors;

/// <summary>
/// Chạy mọi <see cref="IValidator{T}"/> của request trước handler. Có lỗi thì trả
/// <see cref="ErrorKind.Validation"/> kèm lỗi theo field và không gọi handler — validation không
/// rải trong handler.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
    public const string ErrorCode = "validation.failed";

    public async Task<TResponse> HandleAsync(
        TRequest request,
        NextHandler<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var validatorList = validators as IReadOnlyCollection<IValidator<TRequest>> ?? validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count == 0)
        {
            return await next();
        }

        var details = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        return TResponse.FromError(Error.Validation(ErrorCode, "Dữ liệu gửi lên không hợp lệ.", details));
    }
}
