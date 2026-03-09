namespace RewardProgram.Application.Contracts.Admin.Products;

public record AdminProductResponse(
    string Id,
    string Name,
    string ProductCode,
    int PointValue,
    decimal Price,
    string? Category,
    int TotalBarcodes,
    int AvailableBarcodes
);
