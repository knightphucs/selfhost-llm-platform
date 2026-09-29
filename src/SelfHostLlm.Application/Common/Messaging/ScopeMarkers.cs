namespace SelfHostLlm.Application.Common.Messaging;

/// <summary>Request thao tác trên dữ liệu của một tenant. <see cref="Behaviors.TenantAccessBehavior{TRequest,TResponse}"/> kiểm quyền truy cập.</summary>
public interface ITenantRequest
{
    Guid TenantId { get; }
}

/// <summary>Request cấp nền tảng (quản lý tenant) — chỉ PlatformAdmin.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040", Justification = "Marker interface cho TenantAccessBehavior.")]
public interface IPlatformRequest
{
}
