using System.Globalization;
using SelfHostLlm.Adapters.Inference;
using SelfHostLlm.Application;
using SelfHostLlm.ControlPlane.Api.Auth;
using SelfHostLlm.ControlPlane.Api.Endpoints;
using SelfHostLlm.ControlPlane.Api.Hosting;
using SelfHostLlm.Persistence;
using Serilog;

// Logger tạm cho giai đoạn khởi động; UseSerilog thay bằng logger đầy đủ. Không dùng
// CreateBootstrapLogger: logger đó chỉ "freeze" được một lần mỗi process, nên vỡ khi nhiều host
// khởi động song song (integration test).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability("controlplane-api");
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddApplication();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddSecretProtection(builder.Configuration);
    builder.Services.AddInferenceAdapters();
    builder.Services.AddPlatformAuth();
    builder.Services.AddHostedService<AdminBootstrapper>();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthEndpoints();
    app.MapControlPlaneApi();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host controlplane-api dừng bất thường");
}
finally
{
    Log.CloseAndFlush();
}
