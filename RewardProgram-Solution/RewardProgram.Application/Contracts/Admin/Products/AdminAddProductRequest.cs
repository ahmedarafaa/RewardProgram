namespace RewardProgram.Application.Contracts.Admin.Products;

public record AdminAddProductRequest(
    string Name,
    string ProductCode,
    int PointValue,
    string? Category
);
