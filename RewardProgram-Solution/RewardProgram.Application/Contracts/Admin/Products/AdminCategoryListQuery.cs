namespace RewardProgram.Application.Contracts.Admin.Products;

public record AdminCategoryListQuery(
    string? Search,
    int Page = 1,
    int PageSize = 20
);
