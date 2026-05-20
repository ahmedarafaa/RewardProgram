namespace RewardProgram.Application.Helpers;

public static class ExcelExportHelper
{
    public const int MaxExportRows = 50_000;
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static bool IsExportRequested(string? export)
        => string.Equals(export, "xlsx", StringComparison.OrdinalIgnoreCase);

    public static string TimestampedFileName(string baseName)
        => $"{baseName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
}

// Row shape for analytics "Summary" sheets that flatten a composite response's
// scalar KPIs into a two-column (label, value) table.
public sealed record KpiRow(string Label, object? Value);
