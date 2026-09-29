using System.Diagnostics.CodeAnalysis;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Common.Messaging;

/// <summary>Một use case (command hoặc query). Kết quả luôn là <see cref="Result"/> hoặc <see cref="Result{T}"/>.</summary>
[SuppressMessage("Design", "CA1040", Justification = "Marker interface mang kiểu kết quả cho dispatcher.")]
public interface IRequest<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
}

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<TResponse> NextHandler<TResponse>();

/// <summary>
/// Bước xử lý bao quanh handler (validation, logging...). Behavior đăng ký trước nằm ngoài cùng.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, NextHandler<TResponse> next, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        where TResponse : Result, IFailureFactory<TResponse>;
}
