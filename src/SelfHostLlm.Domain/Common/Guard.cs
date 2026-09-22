namespace SelfHostLlm.Domain.Common;

/// <summary>Kiểm tra đầu vào dùng chung cho factory của entity; trả <see cref="Error"/> thay vì ném.</summary>
internal static class Guard
{
    public static Error? NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? Error.Validation($"{field}.empty", $"{field} không được rỗng.") : null;

    public static Error? NotBlank(string? value, string field, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation($"{field}.empty", $"{field} không được rỗng.");
        }

        return value.Length > maxLength
            ? Error.Validation($"{field}.too_long", $"{field} dài tối đa {maxLength} ký tự.")
            : null;
    }

    public static Error? Positive(long value, string field) =>
        value <= 0 ? Error.Validation($"{field}.not_positive", $"{field} phải lớn hơn 0.") : null;

    public static Error? NonNegative(long value, string field) =>
        value < 0 ? Error.Validation($"{field}.negative", $"{field} không được âm.") : null;

    /// <summary>Trả lỗi đầu tiên khác null, hoặc null nếu mọi kiểm tra đều qua.</summary>
    public static Error? First(params Error?[] errors) => errors.FirstOrDefault(e => e is not null);
}
