namespace RewardProgram.Application.Interfaces;

public interface IExcelExporter
{
    Task WriteAsync<T>(
        Stream output,
        IEnumerable<T> rows,
        string sheetName,
        IReadOnlyList<ExcelColumn<T>> columns,
        CancellationToken ct = default);

    // Multi-sheet variant. Caller adds sheets via the builder callback; each call
    // to AddSheet<T> creates an additional worksheet in the same workbook.
    // Used by analytics endpoints that bundle several embedded tables together.
    Task WriteMultiSheetAsync(
        Stream output,
        Action<IExcelWorkbookBuilder> build,
        CancellationToken ct = default);
}

public sealed record ExcelColumn<T>(string Header, Func<T, object?> ValueSelector);

public interface IExcelWorkbookBuilder
{
    IExcelWorkbookBuilder AddSheet<T>(
        string sheetName,
        IEnumerable<T> rows,
        IReadOnlyList<ExcelColumn<T>> columns);
}
