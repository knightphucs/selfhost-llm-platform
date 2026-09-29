using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace SelfHostLlm.ControlPlane.Api.Hosting;

/// <summary>
/// Wiring observability cho host: Serilog, OpenTelemetry (bật/tắt bằng <c>Otel:Enabled</c>)
/// và health check. Mỗi host giữ một bản riêng — cố ý lặp lại thay vì thêm project dùng chung.
/// </summary>
internal static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, logger) =>
        {
            logger
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                // TenantId / ConsumerId / TraceId được đẩy vào LogContext bởi middleware ở các bước sau.
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName);

            if (context.HostingEnvironment.IsProduction())
            {
                logger.WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                logger.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
            }

            logger.WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine("logs", $"{serviceName}-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14);
        });

        if (builder.Configuration.GetValue<bool>("Otel:Enabled"))
        {
            var endpoint = new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317");

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = endpoint))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = endpoint));
        }

        // Chuỗi rỗng cũng coi là thiếu — appsettings không chứa connection string, secret nằm ngoài repo.
        var connectionString = builder.Configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:Postgres (dev: dotnet user-secrets; prod: biến môi trường ConnectionStrings__Postgres).");
        }

        builder.Services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        return builder;
    }

    /// <summary>
    /// <c>/health/live</c>: process còn sống, không gọi dependency.
    /// <c>/health/ready</c>: sẵn sàng phục vụ — kiểm tra các check gắn tag <c>ready</c>.
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Health mở ẩn danh — fallback policy của API yêu cầu đăng nhập.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
        return app;
    }
}
