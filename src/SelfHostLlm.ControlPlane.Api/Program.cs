using System.Globalization;
using SelfHostLlm.ControlPlane.Api.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability("controlplane-api");
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.MapHealthEndpoints();

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
