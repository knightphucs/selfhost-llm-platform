using Microsoft.Extensions.DependencyInjection;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.UnitTests.Fakes;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Security;

public sealed class TenantAccessBehaviorTests
{
    public sealed record ReadTenantData(Guid TenantId) : IRequest<Result>, ITenantRequest;

    public sealed record ManageTenants : IRequest<Result>, IPlatformRequest;

    public sealed record Unscoped : IRequest<Result>;

    public sealed class CountingHandler(Counter counter)
        : IRequestHandler<ReadTenantData, Result>, IRequestHandler<ManageTenants, Result>, IRequestHandler<Unscoped, Result>
    {
        public Task<Result> HandleAsync(ReadTenantData request, CancellationToken cancellationToken) => Handle();

        public Task<Result> HandleAsync(ManageTenants request, CancellationToken cancellationToken) => Handle();

        public Task<Result> HandleAsync(Unscoped request, CancellationToken cancellationToken) => Handle();

        private Task<Result> Handle()
        {
            counter.Calls++;
            return Task.FromResult(Result.Success());
        }
    }

    public sealed class Counter
    {
        public int Calls { get; set; }
    }

    private readonly FakeActor _actor = new();
    private readonly Counter _counter = new();

    private IDispatcher Dispatcher()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<ICurrentActor>(_actor);
        services.AddSingleton(_counter);
        services.AddScoped<IRequestHandler<ReadTenantData, Result>, CountingHandler>();
        services.AddScoped<IRequestHandler<ManageTenants, Result>, CountingHandler>();
        services.AddScoped<IRequestHandler<Unscoped, Result>, CountingHandler>();
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task SendAsync_RequestForOwnTenant_ReachesHandler()
    {
        var result = await Dispatcher().SendAsync(new ReadTenantData(_actor.TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _counter.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_RequestForOtherTenant_IsForbiddenBeforeHandler()
    {
        var result = await Dispatcher().SendAsync(new ReadTenantData(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("tenant_access.forbidden");
        _counter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_PlatformAdminOnOtherTenant_IsAllowed()
    {
        _actor.IsPlatformAdmin = true;

        var result = await Dispatcher().SendAsync(new ReadTenantData(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_EmptyTenantId_ReturnsValidation()
    {
        var result = await Dispatcher().SendAsync(new ReadTenantData(Guid.Empty), CancellationToken.None);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        _counter.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(false, ErrorKind.Forbidden)]
    [InlineData(true, null)]
    public async Task SendAsync_PlatformRequest_OnlyPlatformAdmin(bool isPlatformAdmin, ErrorKind? expectedError)
    {
        _actor.IsPlatformAdmin = isPlatformAdmin;

        var result = await Dispatcher().SendAsync(new ManageTenants(), CancellationToken.None);

        result.Error?.Kind.Should().Be(expectedError);
        _counter.Calls.Should().Be(expectedError is null ? 1 : 0);
    }

    [Fact]
    public async Task SendAsync_UnscopedRequest_IsNotChecked()
    {
        (await Dispatcher().SendAsync(new Unscoped(), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }
}
