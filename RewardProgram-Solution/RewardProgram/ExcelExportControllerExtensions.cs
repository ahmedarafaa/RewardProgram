using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.API;

public static class ExcelExportControllerExtensions
{
    // Stream the workbook into a MemoryStream, then hand it to FileStreamResult so
    // ASP.NET Core writes it to the wire and disposes the stream — one buffer copy
    // total, vs. the previous MemoryStream→ToArray()→FileContentResult which paid
    // for two large allocations on the LOH for every export.
    public static async Task<IActionResult> ExportXlsxAsync<T>(
        this ControllerBase controller,
        IExcelExporter exporter,
        Result<List<T>> result,
        string sheetName,
        string fileBaseName,
        IReadOnlyList<ExcelColumn<T>> columns,
        CancellationToken ct)
    {
        if (result.IsFailure)
            return result.ToProblem();

        var stream = new MemoryStream();
        await exporter.WriteAsync(stream, result.Value, sheetName, columns, ct);
        return BuildFileResponse(controller, stream, fileBaseName);
    }

    // Multi-sheet variant for analytics endpoints that bundle KPI summaries and
    // embedded tables into a single workbook. The caller's `build` callback adds
    // one or more sheets via the builder — works for any number of heterogeneous
    // table shapes without forcing them through a single generic parameter.
    public static async Task<IActionResult> ExportMultiSheetXlsxAsync<TResponse>(
        this ControllerBase controller,
        IExcelExporter exporter,
        Result<TResponse> result,
        string fileBaseName,
        Action<IExcelWorkbookBuilder, TResponse> build,
        CancellationToken ct)
    {
        if (result.IsFailure)
            return result.ToProblem();

        var stream = new MemoryStream();
        await exporter.WriteMultiSheetAsync(stream, b => build(b, result.Value), ct);
        return BuildFileResponse(controller, stream, fileBaseName);
    }

    private static FileStreamResult BuildFileResponse(
        ControllerBase controller, MemoryStream stream, string fileBaseName)
    {
        stream.Position = 0;

        // Exports contain PII (mobiles, customer codes, SAR amounts, scan coordinates).
        // Force no-store so a shared/forward proxy or browser back-button can't surface
        // a stale snapshot after the admin's permissions or the data have changed.
        controller.Response.Headers[HeaderNames.CacheControl] = "no-store, no-cache, must-revalidate";
        controller.Response.Headers[HeaderNames.Pragma] = "no-cache";

        return controller.File(
            stream,
            ExcelExportHelper.ContentType,
            ExcelExportHelper.TimestampedFileName(fileBaseName));
    }
}
