using System.Globalization;
using SelfHostLlm.Adapters.Inference;
using SelfHostLlm.Application;
using SelfHostLlm.Gateway;
using SelfHostLlm.Gateway.Hosting;
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

    builder.AddObservability("gateway");

    // Thứ tự quan trọng: AddGateway đăng ký baseline quota từ snapshot trước khi AddApplicationCore
    // đăng ký bản mặc định (TryAdd). Gateway chỉ cần phần lõi — không có use case quản trị.
    builder.Services.AddGateway();
    builder.Services.AddApplicationCore();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddSecretProtection(builder.Configuration);
    builder.Services.AddInferenceAdapters();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthEndpoints();
    app.MapGateway();

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
