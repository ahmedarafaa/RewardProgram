namespace RewardProgram.Application.Contracts.Admin.Products;

public record AdminEditProductRequest(
    string Name,
    string ProductCode,
    int PointValue,
    string? Category
);
