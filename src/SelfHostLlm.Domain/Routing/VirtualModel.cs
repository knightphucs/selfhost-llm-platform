using System.Text.RegularExpressions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Routing;

/// <summary>
/// Tên model ảo mà client gọi (<c>code-fast</c>, <c>chat-general</c>, <c>embed</c>).
/// Gateway ánh xạ sang deployment thật qua các <see cref="Route"/>.
/// </summary>
public sealed partial class VirtualModel : Entity<Guid>, ITenantScoped
{
    private VirtualModel()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public TaskKind Task { get; private set; }

    public string? Description { get; private set; }

    public static Result<VirtualModel> Create(Guid tenantId, string name, TaskKind task, string? description)
    {
        var virtualModel = new VirtualModel { Id = Guid.NewGuid(), TenantId = tenantId };
        var error = Guard.NotEmpty(tenantId, "virtual_model.tenant_id") ?? virtualModel.Apply(name, task, description);
        return error is null ? virtualModel : error;
    }

    public Result Update(string name, TaskKind task, string? description)
    {
        var error = Apply(name, task, description);
        return error is null ? Result.Success() : error;
    }

    private Error? Apply(string name, TaskKind task, string? description)
    {
        var error = Guard.First(
            NamePattern().IsMatch(name ?? string.Empty)
                ? null
                : Error.Validation(
                    "virtual_model.name.invalid",
                    "Tên virtual model chỉ gồm a-z, 0-9, '-', bắt đầu bằng chữ/số, dài 2–64 ký tự."),
            Enum.IsDefined(task) ? null : Error.Validation("virtual_model.task.invalid", "TaskKind không hợp lệ."));
        if (error is not null)
        {
            return error;
        }

        Name = name!;
        Task = task;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$")]
    private static partial Regex NamePattern();
}
