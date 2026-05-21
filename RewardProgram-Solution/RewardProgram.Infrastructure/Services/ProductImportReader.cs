using System.Globalization;
using ClosedXML.Excel;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

/// <summary>
/// ClosedXML-backed <see cref="IProductImportReader"/>. Columns are located by
/// matching the first row's header text (Arabic or English) against a set of
/// known aliases — the column ORDER in the uploaded file does not matter. A file
/// exported from this app and a code-first ERP export both import correctly,
/// because each is matched by header name rather than by position.
/// </summary>
public class ProductImportReader : IProductImportReader
{
    // Header aliases, all compared after Normalize() (trim + lower-invariant +
    // single-spaced). Each set covers this app's export header plus the ERP
    // file's Arabic headers. Sets are kept disjoint so a header matches one field.
    private static readonly HashSet<string> NameAliases = new(StringComparer.Ordinal)
    {
        "name", "product name", "الاسم", "اسم الصنف", "اسم المنتج", "اسم"
    };

    private static readonly HashSet<string> CodeAliases = new(StringComparer.Ordinal)
    {
        "product code", "productcode", "code", "كود المنتج", "كود الصنف",
        "رمز المنتج", "رمز الصنف", "الكود", "كود"
    };

    private static readonly HashSet<string> CategoryAliases = new(StringComparer.Ordinal)
    {
        "category", "الفئة", "المجموعة", "التصنيف"
    };

    private static readonly HashSet<string> PointsAliases = new(StringComparer.Ordinal)
    {
        "point value", "pointvalue", "points", "قيمة النقاط", "النقاط", "نقاط"
    };

    private static readonly HashSet<string> PriceAliases = new(StringComparer.Ordinal)
    {
        "price (sar)", "price", "السعر (ر.س)", "السعر (ريال)", "السعر", "سعر"
    };

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

            var name = ReadCell(row.Cell(columns.Name));
            var code = ReadCodeCell(row.Cell(columns.ProductCode));
            var category = columns.Category is int categoryColumn
                ? ReadCell(row.Cell(categoryColumn))
                : string.Empty;
            var pointValue = ReadCell(row.Cell(columns.PointValue));
            var price = ReadCell(row.Cell(columns.Price));

            // Skip rows that are entirely blank.
            if (name.Length == 0 && code.Length == 0 && category.Length == 0
                && pointValue.Length == 0 && price.Length == 0)
                continue;

            result.Add(new ProductImportRow(
                row.RowNumber(), name, code, category, pointValue, price));

            // Stop one row past the cap so the caller can reject an oversized
            // file without us materializing an unbounded list.
            if (result.Count > maxRows)
                break;
        }

        return result;
    }

    // Locates each field's column by header text. Required columns are Name,
    // ProductCode, PointValue and Price; Category is optional. Throws
    // ProductImportHeaderException listing any required column not found, so a
    // file in the wrong layout is rejected instead of silently mismapped.
    private static ResolvedColumns ResolveColumns(IXLRow headerRow)
    {
        int? name = null, code = null, category = null, points = null, price = null;

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = Normalize(cell.GetString());
            if (header.Length == 0)
                continue;

            var column = cell.Address.ColumnNumber;
            if (name is null && NameAliases.Contains(header)) name = column;
            else if (code is null && CodeAliases.Contains(header)) code = column;
            else if (category is null && CategoryAliases.Contains(header)) category = column;
            else if (points is null && PointsAliases.Contains(header)) points = column;
            else if (price is null && PriceAliases.Contains(header)) price = column;
        }

        var missing = new List<string>();
        if (name is null) missing.Add("Name / الاسم");
        if (code is null) missing.Add("Product Code / كود المنتج");
        if (points is null) missing.Add("Point Value / قيمة النقاط");
        if (price is null) missing.Add("Price / السعر");

        if (missing.Count > 0)
            throw new ProductImportHeaderException(missing);

        return new ResolvedColumns(name!.Value, code!.Value, category, points!.Value, price!.Value);
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

    private readonly record struct ResolvedColumns(
        int Name, int ProductCode, int? Category, int PointValue, int Price);
}
