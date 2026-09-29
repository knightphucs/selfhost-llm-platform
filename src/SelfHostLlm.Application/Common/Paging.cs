namespace SelfHostLlm.Application.Common;

/// <summary>Trang bắt đầu từ 1. Validator giới hạn <see cref="PageSize"/> tối đa <see cref="MaxPageSize"/>.</summary>
public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 200;

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
