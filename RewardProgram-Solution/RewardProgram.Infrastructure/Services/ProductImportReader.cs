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
    public IReadOnlyList<ProductImportRow> Read(Stream xlsxStream)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var sheet = workbook.Worksheets.First();

        var rows = new List<ProductImportRow>();
        foreach (var row in sheet.RowsUsed())
        {
            // Row 1 is the header.
            if (row.RowNumber() == 1)
                continue;

            var name = ReadCell(row.Cell(1));
            var code = ReadCell(row.Cell(2));
            var category = ReadCell(row.Cell(3));
            var pointValue = ReadCell(row.Cell(4));
            var price = ReadCell(row.Cell(5));

            // Skip rows that are entirely blank.
            if (name.Length == 0 && code.Length == 0 && category.Length == 0
                && pointValue.Length == 0 && price.Length == 0)
                continue;

            rows.Add(new ProductImportRow(
                row.RowNumber(), name, code, category, pointValue, price));
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
}
