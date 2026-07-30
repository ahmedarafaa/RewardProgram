namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>
/// Edit payload for a product. ProductCode is intentionally absent — it is the
/// ERP key and the Excel-import match key, and is immutable. Price is absent too
/// (managed only via Excel import).
/// </summary>
/// <param name="NameEn">
/// English name. Omitting it (null) leaves the stored value untouched, so a client
/// that doesn't yet send the field cannot silently wipe the ERP English catalogue;
/// send an empty string to clear it deliberately.
/// </param>
public record AdminEditProductRequest(
    string Name,
    int PointValue,
    string? Category,
    string? NameEn = null
);
