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
    /// fully blank rows are ignored). Reading stops once <paramref name="maxRows"/>
    /// data rows plus one extra have been collected, so the caller can reject an
    /// oversized file without the reader materializing an unbounded list. Throws
    /// if the stream is not a readable .xlsx workbook.
    /// </summary>
    IReadOnlyList<ProductImportRow> Read(Stream xlsxStream, int maxRows);
}
