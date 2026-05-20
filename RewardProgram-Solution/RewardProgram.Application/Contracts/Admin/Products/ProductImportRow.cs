namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>
/// One raw row parsed from a product-import .xlsx file. Cell values are kept as
/// strings so <c>AdminProductService</c> can validate them and report precise
/// per-row errors. Column order matches the product export.
/// </summary>
public record ProductImportRow(
    int RowNumber,
    string? Name,
    string? ProductCode,
    string? Category,
    string? PointValue,
    string? Price
);
