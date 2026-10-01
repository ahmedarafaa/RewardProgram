using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Contracts.Admin.Products;

namespace RewardProgram.Application.Contracts.Admin.Imports;

/// <summary>
/// A line of general guidance on a template's instructions sheet: a resource key
/// plus any values formatted into it (row caps, size limits).
/// </summary>
public sealed record ImportTemplateNote(string Key, params object[] Arguments);

/// <summary>
/// Everything that distinguishes one downloadable import template from another.
/// The workbook itself is assembled by <c>ImportTemplateBuilder</c>, which is shared.
/// </summary>
/// <param name="FileName">
/// Fixed download name (no timestamp): the frontend surfaces it verbatim, and a
/// stable name means a re-downloaded template overwrites the previous copy instead
/// of littering the user's downloads folder with dated duplicates.
/// </param>
/// <param name="SheetNameKey">Resource key for the data sheet's name.</param>
public sealed record ImportTemplateDefinition(
    string FileName,
    string SheetNameKey,
    IReadOnlyList<ImportColumn> Columns,
    IReadOnlyList<ImportTemplateNote> Notes);

public static class ImportTemplates
{
    public static readonly ImportTemplateDefinition Products = new(
        FileName: "products-import-template.xlsx",
        SheetNameKey: "Export.Sheet.Products",
        Columns: ProductImportColumns.All,
        Notes:
        [
            new("ImportTemplate.Note.HeaderRow"),
            new("ProductImport.Template.Note.Matching"),
            new("ImportTemplate.Note.FirstSheet"),
            new("ImportTemplate.Note.Limits",
                ImportLimits.MaxProductRows, ImportLimits.MaxFileMegabytes),
        ]);

    public static readonly ImportTemplateDefinition ErpCustomers = new(
        FileName: "erp-customers-import-template.xlsx",
        SheetNameKey: "Export.Sheet.ErpCustomers",
        Columns: ErpCustomerImportColumns.All,
        Notes:
        [
            new("ImportTemplate.Note.HeaderRow"),
            new("ErpCustomerImport.Template.Note.Matching"),
            new("ImportTemplate.Note.FirstSheet"),
            new("ImportTemplate.Note.Limits",
                ImportLimits.MaxErpCustomerRows, ImportLimits.MaxFileMegabytes),
        ]);
}
