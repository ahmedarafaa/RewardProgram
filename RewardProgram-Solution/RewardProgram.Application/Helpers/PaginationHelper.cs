namespace RewardProgram.Application.Helpers;

public static class PaginationHelper
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize, int maxPageSize = 100)
        => (Math.Max(1, page), Math.Clamp(pageSize, 1, maxPageSize));
}
