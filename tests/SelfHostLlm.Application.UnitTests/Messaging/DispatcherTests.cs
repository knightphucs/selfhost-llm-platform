using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Messaging;

public sealed class DispatcherTests
{
    public sealed record Greet(string Name) : IRequest<Result<string>>;

    public sealed record Unhandled : IRequest<Result>;

    public sealed class GreetHandler(List<string> trace) : IRequestHandler<Greet, Result<string>>
    {
        public Task<Result<string>> HandleAsync(Greet request, CancellationToken cancellationToken)
        {
            trace.Add("handler");
            return Task.FromResult(Result<string>.Success($"Xin chào {request.Name}"));
        }
    }

    public sealed class GreetValidator : AbstractValidator<Greet>
    {
        public GreetValidator() => RuleFor(r => r.Name).NotEmpty().WithMessage("Tên không được rỗng.");
    }

    public sealed class TracingBehavior(List<string> trace, string label) : IPipelineBehavior<Greet, Result<string>>
    {
        public async Task<Result<string>> HandleAsync(Greet request, NextHandler<Result<string>> next, CancellationToken cancellationToken)
        {
            trace.Add($"{label}:before");
            var result = await next();
            trace.Add($"{label}:after");
            return result;
        }
    }

    private static (IDispatcher Dispatcher, List<string> Trace) Build(Action<IServiceCollection>? configure = null)
    {
        var trace = new List<string>();
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(trace);
        services.AddSingleton<ICurrentActor>(new FakeActor());
        services.AddScoped<IRequestHandler<Greet, Result<string>>, GreetHandler>();
        services.AddScoped<IValidator<Greet>, GreetValidator>();
        configure?.Invoke(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return (provider.CreateScope().ServiceProvider.GetRequiredService<IDispatcher>(), trace);
    }

    [Fact]
    public async Task SendAsync_ValidRequest_InvokesHandler()
    {
        var (dispatcher, trace) = Build();

        var result = await dispatcher.SendAsync(new Greet("Phúc"), CancellationToken.None);

        result.Value.Should().Be("Xin chào Phúc");
        trace.Should().Equal("handler");
    }

    [Fact]
    public async Task SendAsync_InvalidRequest_ReturnsValidationErrorWithoutCallingHandler()
    {
        var (dispatcher, trace) = Build();

        var result = await dispatcher.SendAsync(new Greet(""), CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be("validation.failed");
        result.Error.Details!["Name"].Should().Equal("Tên không được rỗng.");
        trace.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WithBehaviors_RunsInRegistrationOrderAroundHandler()
    {
        var (dispatcher, trace) = Build(services =>
        {
            services.AddScoped<IPipelineBehavior<Greet, Result<string>>>(sp => new TracingBehavior(sp.GetRequiredService<List<string>>(), "A"));
            services.AddScoped<IPipelineBehavior<Greet, Result<string>>>(sp => new TracingBehavior(sp.GetRequiredService<List<string>>(), "B"));
        });

        await dispatcher.SendAsync(new Greet("x"), CancellationToken.None);

        trace.Should().Equal("A:before", "B:before", "handler", "B:after", "A:after");
    }

    [Fact]
    public async Task SendAsync_WithoutRegisteredHandler_ThrowsInvalidOperation()
    {
        var (dispatcher, _) = Build();

        var act = () => dispatcher.SendAsync(new Unhandled(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unhandled*");
    }
}
