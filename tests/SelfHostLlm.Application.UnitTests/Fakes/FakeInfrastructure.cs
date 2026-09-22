using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;
using SelfHostLlm.Domain.Tenancy;

namespace SelfHostLlm.Application.UnitTests.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>Đặt để mô phỏng lỗi ràng buộc DB ở lần lưu kế tiếp.</summary>
    public Error? NextError { get; set; }

    public Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        var error = NextError;
        NextError = null;
        return Task.FromResult(error is null ? Result.Success() : Result.Failure(error));
    }
}

internal sealed class FakeTenantStore : ITenantStore
{
    public List<Tenant> Items { get; } = [];

    public Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Tenant>>(Items.ToList());

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(t => t.Slug == slug));

    public void Add(Tenant tenant) => Items.Add(tenant);
}

/// <summary>"Mã hoá" có thể nhận ra trong test: tiền tố ENC: + chuỗi đảo ngược.</summary>
internal sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => "ENC:" + new string(plaintext.Reverse().ToArray());

    public string? Unprotect(string ciphertext) =>
        ciphertext.StartsWith("ENC:", StringComparison.Ordinal) ? new string(ciphertext[4..].Reverse().ToArray()) : null;
}
