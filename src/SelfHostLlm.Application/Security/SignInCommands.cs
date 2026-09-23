using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Application.Auditing;
using SelfHostLlm.Application.Common;
using SelfHostLlm.Application.Common.Messaging;
using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Audit;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.Security;

/// <summary>
/// Ghi audit đăng nhập thành công. Lúc này request chưa mang danh tính nên actor lấy từ tham số
/// thay vì <see cref="ICurrentActor"/>.
/// </summary>
public sealed record RecordSignInCommand(Guid TenantId, Guid UserId) : IRequest<Result>;

internal sealed class RecordSignInHandler(IAuditLogWriter writer, ICurrentActor actor, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<RecordSignInCommand, Result>
{
    public async Task<Result> HandleAsync(RecordSignInCommand request, CancellationToken cancellationToken)
    {
        var log = AuditLog.Create(
            request.TenantId, request.UserId, AuditAction.Login, "AppUser", request.UserId.ToString(),
            null, null, actor.IpAddress, timeProvider.GetUtcNow());
        if (log.IsFailure)
        {
            return log.Error!;
        }

        writer.Add(log.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Tạo tenant <c>platform</c> và PlatformAdmin đầu tiên — CHỈ khi hệ thống chưa có user nào.
/// Gọi từ host lúc khởi động; có user rồi thì bỏ qua (trả <c>false</c>), nên chạy lại vô hại.
/// </summary>
public sealed record BootstrapPlatformAdminCommand(string Username, string? Email, string Password) : IRequest<Result<bool>>
{
    public const string PlatformTenantSlug = "platform";

    public override string ToString() => $"{nameof(BootstrapPlatformAdminCommand)} {{ Username = {Username}, Password = *** }}";
}

internal sealed class BootstrapPlatformAdminHandler(
    IUserDirectory users,
    ITenantStore tenants,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<BootstrapPlatformAdminCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(BootstrapPlatformAdminCommand request, CancellationToken cancellationToken)
    {
        if (await users.AnyUserAsync(cancellationToken))
        {
            return false;
        }

        var tenant = await tenants.FindBySlugAsync(BootstrapPlatformAdminCommand.PlatformTenantSlug, cancellationToken);
        if (tenant is null)
        {
            var created = Tenant.Create("Platform", BootstrapPlatformAdminCommand.PlatformTenantSlug, timeProvider.GetUtcNow());
            if (created.IsFailure)
            {
                return created.Error!;
            }

            tenant = created.Value;
            tenants.Add(tenant);
            audit.Record(tenant.Id, AuditAction.Create, nameof(Tenant), tenant.Id.ToString(), null, AuditSnapshot.Of(tenant));
        }

        var admin = await users.CreateAsync(tenant.Id, request.Username, request.Email, request.Password, [SystemRoles.PlatformAdmin], cancellationToken);
        if (admin.IsFailure)
        {
            return admin.Error!;
        }

        audit.Record(tenant.Id, AuditAction.Create, "AppUser", admin.Value.Id.ToString(), null, AuditSnapshot.Of(admin.Value));
        return await unitOfWork.SaveAndReturnAsync(true, cancellationToken);
    }
}
