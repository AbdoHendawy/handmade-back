namespace Handmade.Application.Common;

public sealed class PagingQuery
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;

    public int NormalizedPage => Page < 1 ? DefaultPage : Page;

    public int NormalizedPageSize =>
        PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
