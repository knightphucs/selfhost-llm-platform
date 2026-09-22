namespace SelfHostLlm.Domain.Common;

internal static class Hex
{
    /// <summary>Chuỗi hex thường đúng độ dài — định dạng lưu SHA-256 trong hệ thống.</summary>
    public static bool IsLowerHex(string? value, int length) =>
        value is not null
        && value.Length == length
        && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
