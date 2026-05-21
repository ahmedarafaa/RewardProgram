using RewardProgram.Application.Contracts.Admin.Products;

namespace RewardProgram.Application.Interfaces;

/// <summary>
/// Parses a product-import .xlsx workbook into raw rows. Columns are located by
/// matching the first row's header text (Arabic or English), so column order
/// does not matter.
/// </summary>
public interface IProductImportReader
{
    /// <summary>
    /// Reads data rows from the first worksheet. The first non-blank row is the
    /// header: each column is matched to a field by its header text, and fully
    /// blank data rows are ignored. Reading stops once <paramref name="maxRows"/>
    /// data rows plus one extra have been collected, so the caller can reject an
    /// oversized file without the reader materializing an unbounded list.
    /// Throws <see cref="Contracts.Admin.Products.ProductImportHeaderException"/>
    /// when a required column header is missing, or a plain exception when the
    /// stream is not a readable .xlsx workbook.
    /// </summary>
    IReadOnlyList<ProductImportRow> Read(Stream xlsxStream, int maxRows);
}
