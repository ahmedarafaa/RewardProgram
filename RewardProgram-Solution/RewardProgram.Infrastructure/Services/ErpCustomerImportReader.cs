using System.Globalization;
using ClosedXML.Excel;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

/// <summary>
/// ClosedXML-backed <see cref="IErpCustomerImportReader"/>. The two columns
/// (CustomerCode, CustomerName) are located by matching the first row's header
/// text (Arabic or English) against known aliases — column order does not matter.
/// </summary>
public class ErpCustomerImportReader : IErpCustomerImportReader
{
    // Header aliases, compared after Normalize() (trim + lower-invariant + single-spaced).
    private static readonly HashSet<string> CodeAliases = new(StringComparer.Ordinal)
    {
        "customer code", "customercode", "code", "كود العميل", "كود الصنف",
        "رمز العميل", "الكود", "كود"
    };

    private static readonly HashSet<string> NameAliases = new(StringComparer.Ordinal)
    {
        "customer name", "customername", "name", "اسم العميل", "اسم الصنف",
        "الاسم", "اسم"
    };

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
            var code = ReadCell(row.Cell(columns.Code));
            var name = ReadCell(row.Cell(columns.Name));

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
    private static (int Code, int Name) ResolveColumns(IXLRow headerRow)
    {
        int? code = null, name = null;

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = Normalize(cell.GetString());
            if (header.Length == 0)
                continue;

            var column = cell.Address.ColumnNumber;
            if (code is null && CodeAliases.Contains(header)) code = column;
            else if (name is null && NameAliases.Contains(header)) name = column;
        }

        var missing = new List<string>();
        if (code is null) missing.Add("Customer Code / كود العميل");
        if (name is null) missing.Add("Customer Name / اسم العميل");

        if (missing.Count > 0)
            throw new ErpCustomerImportHeaderException(missing);

        return (code!.Value, name!.Value);
    }

    // Trim, lower-case (invariant) and collapse internal whitespace runs so
    // header matching tolerates casing and stray spaces.
    private static string Normalize(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
            return string.Empty;

        return string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
