namespace RewardProgram.Application.Contracts.Admin.Products;

public record AdminProductListQuery(
    string? Search,
    string? Category,
    int Page = 1,
    int PageSize = 20
);
