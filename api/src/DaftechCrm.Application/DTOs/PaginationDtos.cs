namespace DaftechCrm.Application.DTOs;

/// <summary>
/// Incoming paging parameters for list endpoints. Bound from query string,
/// e.g. GET /api/tickets?page=2&amp;pageSize=25.
/// </summary>
public class PaginationQuery
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are clamped to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Items per page. Clamped to [1, 100] to protect the API from unbounded queries.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public int Skip => (Page - 1) * PageSize;

    /// <summary>Optional free-text filter. Each endpoint decides which field(s) it matches against (e.g. Clients matches Name); ignored by endpoints that don't support search.</summary>
    public string? Search { get; set; }
}

/// <summary>
/// A single page of results plus enough metadata for the client to render
/// pager controls without a second round trip.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
