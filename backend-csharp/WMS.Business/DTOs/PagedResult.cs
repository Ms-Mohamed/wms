namespace WMS.Business.DTOs;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>Shared limits so no endpoint can return an unbounded list.</summary>
public static class Paging
{
    public const int DefaultPageSize = 500;
    public const int MaxPageSize = 500;

    public static (int page, int pageSize) Normalize(int? page, int? pageSize)
        => (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
