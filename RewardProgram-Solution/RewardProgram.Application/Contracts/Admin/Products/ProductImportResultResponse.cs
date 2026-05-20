namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>
/// Outcome of a product-import run. Valid rows are upserted (created or updated
/// by ProductCode); invalid rows are skipped and listed in <see cref="Errors"/>.
/// </summary>
public record ProductImportResultResponse(
    int TotalRows,
    int Created,
    int Updated,
    int Failed,
    List<ProductImportRowError> Errors
);
