using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Common.Messaging;

/// <summary>
/// Dispatcher tối giản thay cho MediatR: tìm handler theo kiểu request và xâu các
/// <see cref="IPipelineBehavior{TRequest,TResponse}"/> quanh nó. Wrapper generic được cache theo
/// kiểu request nên reflection chỉ chạy một lần cho mỗi kiểu.
/// </summary>
internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        where TResponse : Result, IFailureFactory<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static requestType => Activator.CreateInstance(
                typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse)))!);

        return wrapper.HandleAsync(request, services, cancellationToken);
    }
}

internal abstract class RequestHandlerWrapper<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
    public abstract Task<TResponse> HandleAsync(object request, IServiceProvider services, CancellationToken cancellationToken);
}

internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IFailureFactory<TResponse>
{
    public override Task<TResponse> HandleAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException($"Chưa đăng ký handler cho {typeof(TRequest).Name}.");

        NextHandler<TResponse> next = () => handler.HandleAsync(typedRequest, cancellationToken);

        // Đảo ngược để behavior đăng ký trước nằm ngoài cùng.
        foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
        {
            var inner = next;
            next = () => behavior.HandleAsync(typedRequest, inner, cancellationToken);
        }

        return next();
    }
}
