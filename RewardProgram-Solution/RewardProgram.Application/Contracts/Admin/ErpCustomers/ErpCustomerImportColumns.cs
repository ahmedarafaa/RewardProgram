using RewardProgram.Application.Contracts.Admin.Imports;

namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>
/// The ERP-customer import column map: both columns are required, and both are
/// matched by header text so column order does not matter. See
/// <see cref="ImportColumns"/> for the matching rules.
/// </summary>
public static class ErpCustomerImportColumns
{
    public static readonly ImportColumn CustomerCode = new(
        canonicalHeader: "Customer Code",
        isRequired: true,
        missingLabel: "Customer Code / كود العميل",
        descriptionKey: "ErpCustomerImport.Template.Description.CustomerCode",
        example: "C001",
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "customer code", "customercode", "code", "كود العميل", "كود الصنف",
            "رمز العميل", "الكود", "كود"
        });

    public static readonly ImportColumn CustomerName = new(
        canonicalHeader: "Customer Name",
        isRequired: true,
        missingLabel: "Customer Name / اسم العميل",
        descriptionKey: "ErpCustomerImport.Template.Description.CustomerName",
        example: "مؤسسة النور التجارية",
        // "name" is here so a sheet exported from this app — whose name header is the
        // generic "Name" — can be edited and uploaded back without renaming a column.
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "customer name", "customername", "name", "اسم العميل", "اسم الصنف",
            "الاسم", "اسم"
        });

    /// <summary>
    /// Both columns, in the order the template writes them — the dashboard's
    /// customers grid order. Short Address is deliberately absent: the importer does
    /// not read it, and a column the parser ignores would look like it was applied.
    /// </summary>
    public static readonly IReadOnlyList<ImportColumn> All = [CustomerCode, CustomerName];
}
