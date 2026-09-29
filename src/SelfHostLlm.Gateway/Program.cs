using System.Globalization;
using SelfHostLlm.Gateway.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability("gateway");
    // TODO(GĐ0 · Gateway): exception handler trả lỗi theo format OpenAI { error: { message, type, code } }.

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.MapHealthEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host gateway dừng bất thường");
}
finally
{
    Log.CloseAndFlush();
}
