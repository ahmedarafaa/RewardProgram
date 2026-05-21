namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>
/// Edit payload for a product. ProductCode is intentionally absent — it is the
/// ERP key and the Excel-import match key, and is immutable. Price is absent too
/// (managed only via Excel import).
/// </summary>
public record AdminEditProductRequest(
    string Name,
    int PointValue,
    string? Category
);
