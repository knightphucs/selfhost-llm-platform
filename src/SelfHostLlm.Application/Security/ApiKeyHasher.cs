using System.Security.Cryptography;
using System.Text;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.Application.Security;

/// <summary>
/// Sinh và băm API key (QĐ-9). Hệ thống chỉ lưu SHA-256 hash + prefix; plaintext chỉ tồn tại
/// trong <see cref="GeneratedApiKey"/> trả về đúng một lần lúc tạo.
/// </summary>
public sealed class ApiKeyHasher
{
    public const string KeyPrefix = "sk-";

    /// <summary>32 byte ngẫu nhiên → 43 ký tự base64url (không padding).</summary>
    private const int SecretBytes = 32;

    public const int KeyLength = 3 + 43;

    /// <summary>Số ký tự đầu của key được lưu làm <c>key_prefix</c> để người dùng nhận ra key.</summary>
    public const int DisplayPrefixLength = 10;

    private static readonly System.Buffers.SearchValues<char> Base64UrlAlphabet =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    public GeneratedApiKey Generate()
    {
        Span<byte> secret = stackalloc byte[SecretBytes];
        RandomNumberGenerator.Fill(secret);

        var plainText = KeyPrefix + Base64UrlEncode(secret);
        return new GeneratedApiKey(plainText, plainText[..DisplayPrefixLength], ComputeHash(plainText));
    }

    /// <summary>SHA-256 dạng hex chữ thường — định dạng mà <see cref="ApiKey"/> lưu trong <c>key_hash</c>.</summary>
    public string ComputeHash(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(plainText), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// So sánh key với hash đã lưu bằng <see cref="CryptographicOperations.FixedTimeEquals"/> —
    /// thời gian so sánh không phụ thuộc vị trí byte khác nhau đầu tiên.
    /// </summary>
    public bool Verify(string plainText, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        if (expectedHash is null || expectedHash.Length != ApiKey.KeyHashLength)
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(plainText), actual);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Kiểm nhanh định dạng trước khi băm — key sai định dạng bị từ chối (401) ngay.</summary>
    public static bool IsWellFormed(string? plainText) =>
        plainText is { Length: KeyLength }
        && plainText.StartsWith(KeyPrefix, StringComparison.Ordinal)
        && plainText.AsSpan(KeyPrefix.Length).IndexOfAnyExcept(Base64UrlAlphabet) < 0;

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Key vừa sinh. <see cref="PlainText"/> chỉ được trả cho client đúng một lần;
/// <see cref="ToString"/> che nó để không lọt vào log.
/// </summary>
public sealed record GeneratedApiKey(string PlainText, string Prefix, string Hash)
{
    public override string ToString() => $"{nameof(GeneratedApiKey)} {{ Prefix = {Prefix}, PlainText = ***, Hash = {Hash} }}";
}
