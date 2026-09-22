namespace SelfHostLlm.Contracts.Admin;

public sealed record CreateTenantRequest(string Name, string Slug);

public sealed record TenantResponse(Guid Id, string Name, string Slug, DateTimeOffset CreatedAt);
