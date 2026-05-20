using ClosedXML.Excel;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

public class ExcelExporter : IExcelExporter
{
    // AdjustToContents measures every cell via GDI text shaping — O(rows*cols) and
    // catastrophic past a few thousand rows. Sampling the first N rows gives a
    // close-enough column width without the perf cliff.
    private const int AdjustToContentsSampleRows = 200;

    // Excel treats cells whose first char is one of these as a formula and may
    // execute the contents on open (CSV/XLSX injection — OWASP-listed). Prefixing
    // with a single quote forces text rendering without changing the visible value.
    private static readonly char[] FormulaTriggerChars = ['=', '+', '-', '@', '\t', '\r'];

    // Excel sheet names are capped at 31 chars and forbid these characters.
    // Localized headers (esp. Arabic) can balloon if a key is rewritten —
    // sanitize defensively so a long/illegal sheet name never throws inside SaveAs.
    private const int MaxSheetNameLength = 31;
    private static readonly char[] ForbiddenSheetNameChars = ['/', '\\', '*', '?', '[', ']', ':'];

    // Default Excel format string for decimal columns. Uses thousands separator
    // and shows up to 2 fractional digits ("1,234.5" / "1,234.56" / "1,234").
    // Applied automatically to any cell whose value is a decimal/double.
    private const string DefaultNumericFormat = "#,##0.##";

    public Task WriteAsync<T>(
        Stream output,
        IEnumerable<T> rows,
        string sheetName,
        IReadOnlyList<ExcelColumn<T>> columns,
        CancellationToken ct = default)
        => Task.Run(() => WriteSingleSheet(output, rows, sheetName, columns, ct), ct);

    public Task WriteMultiSheetAsync(
        Stream output,
        Action<IExcelWorkbookBuilder> build,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var builder = new WorkbookBuilder(workbook, ct);
            build(builder);
            // ClosedXML rejects a workbook with zero worksheets — add a blank fallback
            // so the file opens cleanly when an analytics response has no sections.
            if (workbook.Worksheets.Count == 0)
                workbook.Worksheets.Add("Sheet1");
            workbook.SaveAs(output);
        }, ct);

    private static void WriteSingleSheet<T>(
        Stream output,
        IEnumerable<T> rows,
        string sheetName,
        IReadOnlyList<ExcelColumn<T>> columns,
        CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));
        WriteSheetContents(sheet, rows, columns, ct);
        workbook.SaveAs(output);
    }

    private static void WriteSheetContents<T>(
        IXLWorksheet sheet,
        IEnumerable<T> rows,
        IReadOnlyList<ExcelColumn<T>> columns,
        CancellationToken ct)
    {
        for (var c = 0; c < columns.Count; c++)
            sheet.Cell(1, c + 1).Value = columns[c].Header;

        var headerRange = sheet.Range(1, 1, 1, columns.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            for (var c = 0; c < columns.Count; c++)
            {
                var value = columns[c].ValueSelector(row);
                var cell = sheet.Cell(rowIndex, c + 1);
                SetCellValue(cell, value);
            }
            rowIndex++;
        }

        var lastRow = rowIndex - 1;
        if (lastRow >= 1)
        {
            var sampleEnd = Math.Min(lastRow, AdjustToContentsSampleRows);
            sheet.Columns().AdjustToContents(1, sampleEnd);
        }
    }

    private sealed class WorkbookBuilder : IExcelWorkbookBuilder
    {
        private readonly XLWorkbook _workbook;
        private readonly CancellationToken _ct;
        private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);

        public WorkbookBuilder(XLWorkbook workbook, CancellationToken ct)
        {
            _workbook = workbook;
            _ct = ct;
        }

        public IExcelWorkbookBuilder AddSheet<T>(
            string sheetName,
            IEnumerable<T> rows,
            IReadOnlyList<ExcelColumn<T>> columns)
        {
            var unique = EnsureUniqueName(SanitizeSheetName(sheetName));
            var sheet = _workbook.Worksheets.Add(unique);
            WriteSheetContents(sheet, rows, columns, _ct);
            return this;
        }

        // ClosedXML throws on duplicate sheet names; suffix collisions with " (n)".
        private string EnsureUniqueName(string name)
        {
            if (_usedNames.Add(name))
                return name;

            for (var i = 2; ; i++)
            {
                var suffix = $" ({i})";
                var trimmed = name.Length + suffix.Length > MaxSheetNameLength
                    ? name[..(MaxSheetNameLength - suffix.Length)]
                    : name;
                var candidate = trimmed + suffix;
                if (_usedNames.Add(candidate))
                    return candidate;
            }
        }
    }

    private static string SanitizeSheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Sheet1";

        var cleaned = name;
        foreach (var ch in ForbiddenSheetNameChars)
            cleaned = cleaned.Replace(ch, '_');

        return cleaned.Length > MaxSheetNameLength
            ? cleaned[..MaxSheetNameLength]
            : cleaned;
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = Blank.Value;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                break;
            case bool b:
                cell.Value = b;
                break;
            case decimal d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = DefaultNumericFormat;
                break;
            case double dbl:
                cell.Value = dbl;
                cell.Style.NumberFormat.Format = DefaultNumericFormat;
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case string s:
                cell.Value = EscapeFormulaTrigger(s);
                break;
            default:
                cell.Value = EscapeFormulaTrigger(value.ToString());
                break;
        }
    }

    private static string? EscapeFormulaTrigger(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return Array.IndexOf(FormulaTriggerChars, text[0]) >= 0
            ? "'" + text
            : text;
    }
}
