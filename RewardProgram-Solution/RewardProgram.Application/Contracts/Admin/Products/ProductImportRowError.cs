namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>A single row that failed product-import validation.</summary>
public record ProductImportRowError(
    int RowNumber,
    string? ProductCode,
    string Message
);
