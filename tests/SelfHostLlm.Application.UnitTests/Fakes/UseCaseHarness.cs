using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Fakes;

/// <summary>
/// Chạy use case qua dispatcher thật (TenantAccessBehavior + ValidationBehavior + handler) với
/// hạ tầng in-memory.
/// </summary>
internal sealed class UseCaseHarness
{
    public static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provider;

    public UseCaseHarness()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ICurrentActor>(Actor);
        services.AddSingleton<IAuditLogWriter>(AuditWriter);
        services.AddSingleton<IUnitOfWork>(UnitOfWork);
        services.AddSingleton<ITenantStore>(Tenants);
        services.AddSingleton<ISecretProtector, FakeSecretProtector>();
        services.AddSingleton(typeof(ITenantRepository<>), typeof(FakeTenantRepository<>));
        services.AddSingleton(UsageQueries);
        services.AddSingleton(AuditLogQueries);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public FakeTimeProvider Clock { get; } = new(Now);

    public FakeActor Actor { get; } = new();

    public FakeAuditLogWriter AuditWriter { get; } = new();

    public IReadOnlyList<AuditLog> AuditLogs => AuditWriter.Logs;

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakeTenantStore Tenants { get; } = new();

    public IUsageQueries UsageQueries { get; } = Substitute.For<IUsageQueries>();

    public IAuditLogQueries AuditLogQueries { get; } = Substitute.For<IAuditLogQueries>();

    public Guid TenantId => Actor.TenantId;

    public FakeTenantRepository<T> Repository<T>()
        where T : Entity<Guid>, ITenantScoped =>
        (FakeTenantRepository<T>)_provider.GetRequiredService<ITenantRepository<T>>();

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        where TResponse : Result, IFailureFactory<TResponse>
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(request, CancellationToken.None);
    }
}
