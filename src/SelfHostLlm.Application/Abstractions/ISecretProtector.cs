namespace SelfHostLlm.Application.Abstractions;

/// <summary>
/// Mã hoá secret lưu trong DB (API key phía engine — QĐ-8). Key ring nằm ngoài DB, nên có bản
/// dump DB cũng không giải mã được.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Null nếu ciphertext hỏng hoặc được mã hoá bằng key ring khác.</summary>
    string? Unprotect(string ciphertext);
}
