using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Admin.Imports;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Helpers;

/// <summary>
/// Builds a downloadable import template: a data sheet carrying nothing but the
/// header row, plus a localized instructions sheet. Headers come from the same
/// column map the matching parser matches against, so a template cannot drift away
/// from what its importer accepts.
/// </summary>
public static class ImportTemplateBuilder
{
    /// <summary>
    /// Adds the template's sheets to <paramref name="builder"/>. The data sheet is
    /// added FIRST and must stay first: the importers read worksheet one, so any
    /// other order would feed them the instructions.
    /// </summary>
    public static void Build(
        IExcelWorkbookBuilder builder,
        IStringLocalizer<ErrorMessages> l,
        ImportTemplateDefinition definition)
    {
        // Header row only — deliberately no sample data row. A template uploaded
        // unchanged must import nothing, not a phantom demo record.
        builder.AddSheet<object>(
            l[definition.SheetNameKey].Value,
            [],
            [.. definition.Columns.Select(c => new ExcelColumn<object>(c.CanonicalHeader, _ => null))]);

        builder.AddSheet(
            l["ImportTemplate.Sheet.Instructions"].Value,
            BuildGuidanceRows(l, definition),
            [
                new ExcelColumn<TemplateGuidanceRow>(l["ImportTemplate.Header.Column"].Value, r => r.Column),
                new ExcelColumn<TemplateGuidanceRow>(l["ImportTemplate.Header.Required"].Value, r => r.Required),
                new ExcelColumn<TemplateGuidanceRow>(l["ImportTemplate.Header.Description"].Value, r => r.Description),
                new ExcelColumn<TemplateGuidanceRow>(l["ImportTemplate.Header.Example"].Value, r => r.Example),
            ]);
    }

    private static List<TemplateGuidanceRow> BuildGuidanceRows(
        IStringLocalizer<ErrorMessages> l, ImportTemplateDefinition definition)
    {
        var required = l["ImportTemplate.Required.Yes"].Value;
        var optional = l["ImportTemplate.Required.No"].Value;

        var rows = definition.Columns
            .Select(c => new TemplateGuidanceRow(
                c.CanonicalHeader,
                c.IsRequired ? required : optional,
                l[c.DescriptionKey].Value,
                c.Example))
            .ToList();

        // General notes follow the per-column table, separated by a blank row and
        // carried in the Description column so they read as a continuous paragraph.
        rows.Add(new TemplateGuidanceRow(string.Empty, string.Empty, string.Empty, string.Empty));
        foreach (var note in definition.Notes)
        {
            var text = note.Arguments.Length == 0
                ? l[note.Key].Value
                : l[note.Key, note.Arguments].Value;

            rows.Add(new TemplateGuidanceRow(string.Empty, string.Empty, text, string.Empty));
        }

        return rows;
    }

    private sealed record TemplateGuidanceRow(
        string Column, string Required, string Description, string Example);
}
