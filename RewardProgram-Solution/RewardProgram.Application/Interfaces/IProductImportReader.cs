using RewardProgram.Application.Contracts.Admin.Products;

namespace RewardProgram.Application.Interfaces;

/// <summary>
/// Parses a product-import .xlsx workbook into raw rows. Column order matches the
/// product export: Name, ProductCode, Category, PointValue, Price.
/// </summary>
public interface IProductImportReader
{
    /// <summary>
    /// Reads data rows from the first worksheet (the header row is skipped, and
    /// fully blank rows are ignored). Throws if the stream is not a readable
    /// .xlsx workbook.
    /// </summary>
    IReadOnlyList<ProductImportRow> Read(Stream xlsxStream);
}
