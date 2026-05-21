using System.Globalization;
using ClosedXML.Excel;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

/// <summary>
/// ClosedXML-backed <see cref="IProductImportReader"/>. Reads the first worksheet,
/// columns A–E: Name, ProductCode, Category, PointValue, Price — the same column
/// order the product export produces, so an exported file can be edited and
/// re-imported directly.
/// </summary>
public class ProductImportReader : IProductImportReader
{
    public IReadOnlyList<ProductImportRow> Read(Stream xlsxStream, int maxRows)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var sheet = workbook.Worksheets.First();

        var rows = new List<ProductImportRow>();
        var headerSkipped = false;

        foreach (var row in sheet.RowsUsed())
        {
            // The first non-blank row is the header, wherever it sits — an
            // exported file may carry blank rows above it.
            if (!headerSkipped)
            {
                headerSkipped = true;
                continue;
            }

            var name = ReadCell(row.Cell(1));
            var code = ReadCodeCell(row.Cell(2));
            var category = ReadCell(row.Cell(3));
            var pointValue = ReadCell(row.Cell(4));
            var price = ReadCell(row.Cell(5));

            // Skip rows that are entirely blank.
            if (name.Length == 0 && code.Length == 0 && category.Length == 0
                && pointValue.Length == 0 && price.Length == 0)
                continue;

            rows.Add(new ProductImportRow(
                row.RowNumber(), name, code, category, pointValue, price));

            // Stop one row past the cap so the caller can reject an oversized
            // file without us materializing an unbounded list.
            if (rows.Count > maxRows)
                break;
        }

        return rows;
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
