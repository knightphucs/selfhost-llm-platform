using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using SelfHostLlm.Application.Abstractions;

namespace SelfHostLlm.Persistence.Security;

/// <summary>
/// Mã hoá secret bằng ASP.NET Core Data Protection (QĐ-8 — không tự chế AES).
/// Purpose string có version để sau này xoay định dạng mà không đụng dữ liệu cũ.
/// </summary>
internal sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    public const string Purpose = "SelfHostLlm.Deployment.ApiKey.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string? Unprotect(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
