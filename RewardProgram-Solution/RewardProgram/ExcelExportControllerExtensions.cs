using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Imports;
using RewardProgram.Application.Errors;
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

    // The blank import workbook for one importer: header row generated from the same
    // column map that importer's parser matches against, so "download, fill, upload"
    // cannot fail on headers.
    public static Task<IActionResult> ImportTemplateAsync(
        this ControllerBase controller,
        IExcelExporter exporter,
        IStringLocalizer<ErrorMessages> localizer,
        ImportTemplateDefinition definition,
        CancellationToken ct)
        => controller.WorkbookFileAsync(
            exporter,
            definition.FileName,
            builder => ImportTemplateBuilder.Build(builder, localizer, definition),
            ct);

    // Workbook download with an exact, caller-supplied file name — for generated
    // documents like the import templates, whose name is part of the contract and
    // must not carry the export endpoints' timestamp suffix.
    public static async Task<IActionResult> WorkbookFileAsync(
        this ControllerBase controller,
        IExcelExporter exporter,
        string fileName,
        Action<IExcelWorkbookBuilder> build,
        CancellationToken ct)
    {
        var stream = new MemoryStream();
        await exporter.WriteMultiSheetAsync(stream, build, ct);
        return BuildFileResponse(controller, stream, fileName, timestamp: false);
    }

    private static FileStreamResult BuildFileResponse(
        ControllerBase controller, MemoryStream stream, string fileBaseName, bool timestamp = true)
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
            timestamp ? ExcelExportHelper.TimestampedFileName(fileBaseName) : fileBaseName);
    }
}
