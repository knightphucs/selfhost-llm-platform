using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Persistence.Configurations.Conventions;

/// <summary>
/// Helper cấu hình dùng chung. Tên cột đặt tường minh bằng snake_case để check constraint
/// và SQL tay luôn khớp với cột thật.
/// </summary>
internal static class EntityTypeBuilderExtensions
{
    private const string TenantIdProperty = nameof(ITenantScoped.TenantId);

    /// <summary>Lưu enum dạng string kèm check constraint <c>ck_&lt;table&gt;_&lt;column&gt;</c>.</summary>
    public static EntityTypeBuilder<T> HasEnumColumn<T, TEnum>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, TEnum>> property,
        string column)
        where T : class
        where TEnum : struct, Enum
    {
        builder.Property(property).HasConversion<string>().HasMaxLength(32).HasColumnName(column);
        return builder.HasInListCheck<T, TEnum>(column);
    }

    /// <inheritdoc cref="HasEnumColumn{T,TEnum}(EntityTypeBuilder{T},Expression{Func{T,TEnum}},string)"/>
    public static EntityTypeBuilder<T> HasEnumColumn<T, TEnum>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, TEnum?>> property,
        string column)
        where T : class
        where TEnum : struct, Enum
    {
        // NULL IN (...) cho ra NULL → check constraint vẫn qua, đúng ý nghĩa cột nullable.
        builder.Property(property).HasConversion<string>().HasMaxLength(32).HasColumnName(column);
        return builder.HasInListCheck<T, TEnum>(column);
    }

    /// <summary>Check constraint cho cột <c>text[]</c> chứa tên enum: mọi phần tử phải hợp lệ.</summary>
    public static EntityTypeBuilder<T> HasEnumArrayCheck<T, TEnum>(this EntityTypeBuilder<T> builder, string column)
        where T : class
        where TEnum : struct, Enum
    {
        var values = string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
        builder.ToTable(t => t.HasCheckConstraint($"ck_{TableName(builder)}_{column}", $"{column} <@ ARRAY[{values}]::text[]"));
        return builder;
    }

    /// <summary>FK <c>tenant_id → tenant(id)</c> + alternate key <c>(tenant_id, id)</c> cho bảng con tham chiếu.</summary>
    public static EntityTypeBuilder<T> HasTenant<T>(this EntityTypeBuilder<T> builder, bool isPrincipal = true)
        where T : class, ITenantScoped
    {
        builder.Property(TenantIdProperty).HasColumnName("tenant_id");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(TenantIdProperty)
            .HasConstraintName($"fk_{TableName(builder)}_tenant")
            .OnDelete(DeleteBehavior.Restrict);
        if (isPrincipal)
        {
            builder.HasAlternateKey(TenantIdProperty, "Id").HasName($"ak_{TableName(builder)}_tenant_id_id");
        }

        return builder;
    }

    /// <summary>
    /// Composite FK <c>(tenant_id, fk) → parent(tenant_id, id)</c>: DB từ chối mọi tham chiếu
    /// tới entity của tenant khác, kể cả khi code tầng trên có bug.
    /// </summary>
    public static EntityTypeBuilder<T> HasTenantForeignKey<T, TParent>(
        this EntityTypeBuilder<T> builder,
        string foreignKeyProperty,
        DeleteBehavior onDelete = DeleteBehavior.Restrict)
        where T : class, ITenantScoped
        where TParent : class, ITenantScoped
    {
        builder.HasOne<TParent>()
            .WithMany()
            .HasForeignKey(TenantIdProperty, foreignKeyProperty)
            .HasPrincipalKey(TenantIdProperty, "Id")
            .HasConstraintName($"fk_{TableName(builder)}_{ToSnakeCase(foreignKeyProperty)}")
            .OnDelete(onDelete);
        return builder;
    }

    private static EntityTypeBuilder<T> HasInListCheck<T, TEnum>(this EntityTypeBuilder<T> builder, string column)
        where T : class
        where TEnum : struct, Enum
    {
        var values = string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
        builder.ToTable(t => t.HasCheckConstraint($"ck_{TableName(builder)}_{column}", $"{column} IN ({values})"));
        return builder;
    }

    /// <summary>
    /// Chuyển PascalCase → snake_case (<c>EmbeddingModelId</c> → <c>embedding_model_id</c>),
    /// khớp cách EFCore.NamingConventions đặt tên cột.
    /// </summary>
    internal static string ToSnakeCase(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static string TableName<T>(EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Metadata.GetTableName()
        ?? throw new InvalidOperationException($"Gọi ToTable(...) trước khi thêm check constraint cho {typeof(T).Name}.");
}
