using System.Globalization;
using ClosedXML.Excel;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Contracts.Admin.Imports;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

/// <summary>
/// ClosedXML-backed <see cref="IErpCustomerImportReader"/>. The two columns
/// (CustomerCode, CustomerName) are located by matching the first row's header text
/// (Arabic or English) against <see cref="ErpCustomerImportColumns"/> — the same map
/// the downloadable template is generated from, so the two can never disagree.
/// Column order does not matter.
/// </summary>
public class ErpCustomerImportReader : IErpCustomerImportReader
{
    public IReadOnlyList<ErpCustomerImportRow> Read(Stream xlsxStream, int maxRows)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var sheet = workbook.Worksheets.First();

        using var rows = sheet.RowsUsed().GetEnumerator();

        // No used rows at all → empty workbook; let the caller report "empty file".
        if (!rows.MoveNext())
            return [];

        var columns = ResolveColumns(rows.Current);

        var result = new List<ErpCustomerImportRow>();
        while (rows.MoveNext())
        {
            var row = rows.Current;
            var code = ReadCell(row.Cell(columns[ErpCustomerImportColumns.CustomerCode]));
            var name = ReadCell(row.Cell(columns[ErpCustomerImportColumns.CustomerName]));

            // Skip rows that are entirely blank.
            if (code.Length == 0 && name.Length == 0)
                continue;

            result.Add(new ErpCustomerImportRow(row.RowNumber(), code, name));

            // Stop one row past the cap so the caller can reject an oversized
            // file without us materializing an unbounded list.
            if (result.Count > maxRows)
                break;
        }

        return result;
    }

    // Locates the two columns by header text. Both are required; throws
    // ErpCustomerImportHeaderException listing any not found.
    private static Dictionary<ImportColumn, int> ResolveColumns(IXLRow headerRow)
    {
        var resolved = ImportColumns.Resolve(
            ErpCustomerImportColumns.All,
            headerRow.CellsUsed().Select(c => (c.GetString(), c.Address.ColumnNumber)));

        var missing = ImportColumns.MissingRequired(ErpCustomerImportColumns.All, resolved);
        if (missing.Count > 0)
            throw new ErpCustomerImportHeaderException(missing);

        return resolved;
    }

    // Code and Name are identifiers/text. A numeric cell is rendered without a
    // decimal point, group separators, or scientific notation so the value is
    // preserved verbatim (e.g. a numeric customer code keeps all its digits).
    private static string ReadCell(IXLCell cell)
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
