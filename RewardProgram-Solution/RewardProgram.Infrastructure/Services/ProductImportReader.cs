using System.Globalization;
using ClosedXML.Excel;
using RewardProgram.Application.Contracts.Admin.Imports;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

/// <summary>
/// ClosedXML-backed <see cref="IProductImportReader"/>. Columns are located by
/// matching the first row's header text (Arabic or English) against
/// <see cref="ProductImportColumns"/> — the same map the downloadable template is
/// generated from, so the two can never disagree. The column ORDER in the uploaded
/// file does not matter: a file exported from this app and a code-first ERP export
/// both import correctly, because each is matched by header name rather than by
/// position.
/// </summary>
public class ProductImportReader : IProductImportReader
{
    public IReadOnlyList<ProductImportRow> Read(Stream xlsxStream, int maxRows)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var sheet = workbook.Worksheets.First();

        using var rows = sheet.RowsUsed().GetEnumerator();

        // No used rows at all → empty workbook; let the caller report "empty file".
        if (!rows.MoveNext())
            return [];

        var columns = ResolveColumns(rows.Current);

        var result = new List<ProductImportRow>();
        while (rows.MoveNext())
        {
            var row = rows.Current;

            var name = ReadCell(row.Cell(columns[ProductImportColumns.Name]));
            var code = ReadCodeCell(row.Cell(columns[ProductImportColumns.ProductCode]));
            var category = columns.TryGetValue(ProductImportColumns.Category, out var categoryColumn)
                ? ReadCell(row.Cell(categoryColumn))
                : string.Empty;
            var pointValue = ReadCell(row.Cell(columns[ProductImportColumns.PointValue]));
            var price = ReadCell(row.Cell(columns[ProductImportColumns.Price]));

            // Left null when the file has no English-name column, which the service
            // reads as "don't touch NameEn" — distinct from a present-but-empty cell,
            // which deliberately clears it.
            var nameEn = columns.TryGetValue(ProductImportColumns.NameEn, out var nameEnColumn)
                ? ReadCell(row.Cell(nameEnColumn))
                : null;

            // Skip rows that are entirely blank.
            if (name.Length == 0 && code.Length == 0 && category.Length == 0
                && pointValue.Length == 0 && price.Length == 0
                && string.IsNullOrEmpty(nameEn))
                continue;

            result.Add(new ProductImportRow(
                row.RowNumber(), name, code, category, pointValue, price, nameEn));

            // Stop one row past the cap so the caller can reject an oversized
            // file without us materializing an unbounded list.
            if (result.Count > maxRows)
                break;
        }

        return result;
    }

    // Locates each field's column by header text. Required columns are Name,
    // ProductCode, PointValue and Price; Category and Name (EN) are optional.
    // Throws ProductImportHeaderException listing any required column not found,
    // so a file in the wrong layout is rejected instead of silently mismapped.
    private static Dictionary<ImportColumn, int> ResolveColumns(IXLRow headerRow)
    {
        var resolved = ImportColumns.Resolve(
            ProductImportColumns.All,
            headerRow.CellsUsed().Select(c => (c.GetString(), c.Address.ColumnNumber)));

        var missing = ImportColumns.MissingRequired(ProductImportColumns.All, resolved);
        if (missing.Count > 0)
            throw new ProductImportHeaderException(missing);

        return resolved;
    }

    // Numeric cells are normalized to an invariant-culture string so the service
    // parses them deterministically regardless of the server's locale.
    private static string ReadCell(IXLCell cell)
    {
        if (cell.IsEmpty())
            return string.Empty;

        return cell.DataType == XLDataType.Number
            ? cell.GetValue<double>().ToString(CultureInfo.InvariantCulture)
            : cell.GetString().Trim();
    }

    // ProductCode is an identifier, never a quantity: a numeric cell is rendered
    // without a decimal point, group separators, or scientific notation so the
    // value still matches the stored ProductCode. (Leading zeros that Excel
    // dropped when the code was typed as a number cannot be recovered here.)
    private static string ReadCodeCell(IXLCell cell)
    {
        if (cell.IsEmpty())
            return string.Empty;

        if (cell.DataType != XLDataType.Number)
            return cell.GetString().Trim();

        var value = cell.GetValue<double>();
        return value == Math.Floor(value) && !double.IsInfinity(value)
            ? value.ToString("F0", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
    }
}
