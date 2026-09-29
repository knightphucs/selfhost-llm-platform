namespace SelfHostLlm.Contracts.Admin.Common;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
