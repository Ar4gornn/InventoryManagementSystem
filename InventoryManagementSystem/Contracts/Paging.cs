using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.Contracts;

/// <summary>
/// One page of results plus what the caller needs to ask for the next one.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// Paging shared by every list endpoint. <see cref="MaxPageSize"/> is a hard cap: a
/// caller asking for a million rows gets the cap, not a timeout.
/// </summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 20;

    [Range(1, int.MaxValue, ErrorMessage = "Page starts at 1.")]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value,
        };
    }
}

public class ProductQuery : PageQuery
{
    /// <summary>Case-insensitive match against SKU or name.</summary>
    public string? Search { get; set; }

    public int? CategoryId { get; set; }
}

public class CategoryQuery : PageQuery
{
    /// <summary>Case-insensitive match against the category name.</summary>
    public string? Search { get; set; }
}
