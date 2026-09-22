namespace SelfHostLlm.Application.Common;

/// <summary>
/// Enum trong command/Contracts là string. Parse theo tên (không phân biệt hoa thường) và
/// KHÔNG nhận chuỗi số — <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/> nhận cả "1".
/// </summary>
public static class EnumText
{
    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        var name = value is null
            ? null
            : Enum.GetNames<TEnum>().FirstOrDefault(n => n.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        result = name is null ? default : Enum.Parse<TEnum>(name);
        return name is not null;
    }

    public static bool IsDefined<TEnum>(string? value)
        where TEnum : struct, Enum => TryParse<TEnum>(value, out _);

    /// <summary>Dùng sau khi validator đã kiểm — gọi với giá trị sai là lỗi lập trình.</summary>
    public static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var result)
            ? result
            : throw new ArgumentException($"'{value}' không phải {typeof(TEnum).Name} hợp lệ.", nameof(value));

    public static string Names<TEnum>()
        where TEnum : struct, Enum => string.Join(", ", Enum.GetNames<TEnum>());
}
