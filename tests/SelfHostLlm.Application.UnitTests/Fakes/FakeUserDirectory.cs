using SelfHostLlm.Application.Abstractions;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Application.UnitTests.Fakes;

internal sealed class FakeUserDirectory : IUserDirectory
{
    public List<UserAccount> Users { get; } = [];

    public Task<bool> AnyUserAsync(CancellationToken cancellationToken) => Task.FromResult(Users.Count > 0);

    public Task<IReadOnlyList<UserAccount>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserAccount>>(Users.Where(u => u.TenantId == tenantId).ToList());

    public Task<UserAccount?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.TenantId == tenantId && u.Id == userId));

    public Task<Result<UserAccount>> CreateAsync(
        Guid tenantId, string username, string? email, string password, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        if (password.Length < 10)
        {
            return Task.FromResult(Result<UserAccount>.Failure(Error.Validation("identity.password", "Mật khẩu yếu.")));
        }

        var user = new UserAccount(Guid.NewGuid(), tenantId, username, email, true, roles.ToList());
        Users.Add(user);
        return Task.FromResult(Result<UserAccount>.Success(user));
    }

    public Task<Result<UserAccount>> SetRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var index = Users.FindIndex(u => u.TenantId == tenantId && u.Id == userId);
        Users[index] = Users[index] with { Roles = roles.ToList() };
        return Task.FromResult(Result<UserAccount>.Success(Users[index]));
    }
}
