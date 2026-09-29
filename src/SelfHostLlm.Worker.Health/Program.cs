using System.Globalization;
using SelfHostLlm.Worker.Health.Hosting;
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

    builder.AddObservability("worker-health");
    // TODO(GĐ1): đăng ký BackgroundService probe health deployment + aggregate usage.
    // Host dùng Microsoft.NET.Sdk.Web chỉ để expose /health/live và /health/ready.

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.MapHealthEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host worker-health dừng bất thường");
}
finally
{
    Log.CloseAndFlush();
}
