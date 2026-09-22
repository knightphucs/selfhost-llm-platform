namespace SelfHostLlm.Domain.Common;

/// <summary>
/// Entity mang dữ liệu người dùng, thuộc về đúng một tenant. Mọi repository method truy vấn
/// entity loại này bắt buộc nhận <c>tenantId</c> — không có overload nào thiếu nó.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
