using System.Text.RegularExpressions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.Tenancy;

/// <summary>Đơn vị cô lập dữ liệu. Là gốc của mọi entity <see cref="ITenantScoped"/>.</summary>
public sealed partial class Tenant : Entity<Guid>
{
    private Tenant()
    {
    }

    public string Name { get; private set; } = null!;

    /// <summary>Định danh dạng URL, duy nhất toàn hệ thống.</summary>
    public string Slug { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Tenant> Create(string name, string slug, DateTimeOffset now)
    {
        var error = Guard.First(
            Guard.NotBlank(name, "tenant.name"),
            SlugPattern().IsMatch(slug ?? string.Empty)
                ? null
                : Error.Validation("tenant.slug.invalid", "Slug chỉ gồm a-z, 0-9, '-' và dài 2–50 ký tự."));
        if (error is not null)
        {
            return error;
        }

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug!,
            CreatedAt = now.ToUniversalTime(),
        };
    }

    [GeneratedRegex("^[a-z0-9-]{2,50}$")]
    private static partial Regex SlugPattern();
}
