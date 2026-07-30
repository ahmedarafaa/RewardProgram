namespace RewardProgram.Application.Contracts.Admin.Products;

public record AdminProductResponse(
    string Id,
    string Name,
    // English catalogue name. Null for the few products with no English entry yet.
    string? NameEn,
    string ProductCode,
    int PointValue,
    decimal Price,
    string? Category,
    int TotalBarcodes,
    int AvailableBarcodes
);
