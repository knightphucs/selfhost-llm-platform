using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common.Behaviors;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Metering;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Application.Security;

namespace SelfHostLlm.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Toàn bộ Application: dịch vụ lõi + dispatcher, pipeline behavior, handler, validator, audit.
    /// Dùng ở ControlPlane — nơi có <see cref="ICurrentActor"/> và Identity.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddApplicationCore();
        services.AddScoped<IDispatcher, Dispatcher>();

        // Thứ tự đăng ký = thứ tự chạy (ngoài vào trong): chặn truy cập chéo tenant trước, rồi mới validate.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantAccessBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        AddRequestHandlers(services, assembly);
        services.AddScoped<IAuditTrail, AuditTrail>();

        return services;
    }

    /// <summary>
    /// Chỉ các dịch vụ lõi của data plane (không use case quản trị): ApiKeyHasher, FallbackExecutor,
    /// quota. Gateway dùng bản này — nó không có danh tính người dùng quản trị.
    /// </summary>
    public static IServiceCollection AddApplicationCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ApiKeyHasher>();
        services.TryAddSingleton<FallbackExecutor>();

        // Quota giữ bộ đếm in-memory nên bắt buộc singleton. Gateway thay baseline bằng bản đọc từ snapshot.
        services.TryAddSingleton<IMonthlyUsageBaseline, NullMonthlyUsageBaseline>();
        services.TryAddSingleton<IQuotaService, InMemoryQuotaService>();

        return services;
    }

    private static void AddRequestHandlers(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var type in handlerTypes)
        {
            foreach (var contract in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            {
                services.AddScoped(contract, type);
            }
        }
    }
}
