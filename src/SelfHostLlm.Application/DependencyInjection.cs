using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Common.Behaviors;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Metering;
using SelfHostLlm.Application.Routing;
using SelfHostLlm.Application.Security;

namespace SelfHostLlm.Application;

public static class DependencyInjection
{
    /// <summary>Đăng ký dispatcher, pipeline behavior, handler, validator và các service lõi.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDispatcher, Dispatcher>();

        // Thứ tự đăng ký = thứ tự chạy (ngoài vào trong): chặn truy cập chéo tenant trước, rồi mới validate.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantAccessBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        AddRequestHandlers(services, assembly);

        // Service lõi — không trạng thái hoặc tự quản lý trạng thái thread-safe.
        services.AddSingleton<ApiKeyHasher>();
        services.AddSingleton<FallbackExecutor>();

        // Quota giữ bộ đếm in-memory nên bắt buộc singleton. Gateway thay baseline bằng bản đọc từ snapshot.
        services.TryAddSingleton<IMonthlyUsageBaseline, NullMonthlyUsageBaseline>();
        services.AddSingleton<IQuotaService, InMemoryQuotaService>();

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
