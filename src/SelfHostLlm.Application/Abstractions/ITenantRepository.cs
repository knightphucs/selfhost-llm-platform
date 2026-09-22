using System.Linq.Expressions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Repository generic cho mọi entity <see cref="ITenantScoped"/>. Mọi method truy vấn bắt buộc
/// nhận <c>tenantId</c> — không có overload nào thiếu nó. Implementation áp bộ lọc tenant ở
/// đúng một chỗ, trước mọi điều kiện khác.
/// </summary>
public interface ITenantRepository<T>
    where T : Entity<Guid>, ITenantScoped
{
    /// <summary>Entity của tenant; <c>null</c> nếu không tồn tại <b>hoặc thuộc tenant khác</b>.</summary>
    Task<T?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> ListAsync(Guid tenantId, Expression<Func<T, bool>>? filter, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid tenantId, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);

    /// <summary>Đưa entity vào unit of work. Ném <see cref="ArgumentException"/> nếu TenantId rỗng.</summary>
    void Add(T entity);

    void Remove(T entity);
}
