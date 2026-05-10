namespace ERP.Application.Common.Pagination;

public sealed record PaginationRequest(int Page = 1, int PageSize = 20)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public int NormalizedPage => Page < 1 ? DefaultPage : Page;

    public int NormalizedPageSize => PageSize switch
    {
        <= 0 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}
