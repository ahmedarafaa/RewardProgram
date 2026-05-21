using RewardProgram.Application.Contracts.Admin.ErpCustomers;

namespace RewardProgram.Application.Interfaces;

/// <summary>
/// Parses an ERP-customer import .xlsx workbook into raw rows. Columns are located
/// by matching the first row's header text (Arabic or English), so column order
/// does not matter.
/// </summary>
public interface IErpCustomerImportReader
{
    /// <summary>
    /// Reads data rows from the first worksheet. The first non-blank row is the
    /// header: each column is matched to a field by its header text, and fully
    /// blank data rows are ignored. Reading stops once <paramref name="maxRows"/>
    /// data rows plus one extra have been collected.
    /// Throws <see cref="ErpCustomerImportHeaderException"/> when a required column
    /// header is missing, or a plain exception when the stream is not a readable
    /// .xlsx workbook.
    /// </summary>
    IReadOnlyList<ErpCustomerImportRow> Read(Stream xlsxStream, int maxRows);
}
