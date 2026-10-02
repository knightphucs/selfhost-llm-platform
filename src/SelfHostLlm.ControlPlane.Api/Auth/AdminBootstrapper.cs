using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Application.Security;
using SelfHostLlm.Persistence;

namespace SelfHostLlm.ControlPlane.Api.Auth;

/// <summary>
/// Tạo PlatformAdmin đầu tiên khi khởi động nếu hệ thống chưa có user. Thông tin lấy từ
/// <c>Bootstrap:AdminUsername/AdminPassword/AdminEmail</c> (user-secrets hoặc biến môi trường —
/// không commit). DB chưa migrate thì chỉ cảnh báo, KHÔNG tự migrate.
/// </summary>
internal sealed partial class AdminBootstrapper(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdminBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var username = configuration["Bootstrap:AdminUsername"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            LogNotConfigured(logger);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await DatabaseStatus.IsSchemaUpToDateAsync(db, cancellationToken))
            {
                LogSchemaNotReady(logger);
                return;
            }

            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var result = await dispatcher.SendAsync(
                new BootstrapPlatformAdminCommand(username, configuration["Bootstrap:AdminEmail"], password),
                cancellationToken);

            if (result.IsFailure)
            {
                // Details là mô tả lỗi của Identity ("Passwords must have...") — không chứa giá trị password.
                var details = result.Error!.Details is { Count: > 0 } d
                    ? string.Join("; ", d.Select(kv => $"{kv.Key}: {string.Join(" ", kv.Value)}"))
                    : "-";
                LogFailed(logger, result.Error.Code, result.Error.Message, details);
            }
            else if (result.Value)
            {
                LogCreated(logger, username);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bootstrap lỗi (DB chưa lên...) không được làm sập control plane.
            LogUnexpected(logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrap admin chưa cấu hình (Bootstrap:AdminUsername/AdminPassword) — bỏ qua")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DB chưa migrate hoặc không kết nối được — bỏ qua bootstrap. Chạy: dotnet ef database update")]
    private static partial void LogSchemaNotReady(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã tạo PlatformAdmin đầu tiên: {Username}")]
    private static partial void LogCreated(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bootstrap admin thất bại: {Code} — {Message} ({Details})")]
    private static partial void LogFailed(ILogger logger, string code, string message, string details);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bootstrap admin gặp lỗi không mong muốn")]
    private static partial void LogUnexpected(ILogger logger, Exception exception);
}
