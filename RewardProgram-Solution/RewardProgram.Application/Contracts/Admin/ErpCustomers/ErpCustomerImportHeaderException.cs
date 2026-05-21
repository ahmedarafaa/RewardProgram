namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>
/// Thrown by <c>IErpCustomerImportReader</c> when the uploaded workbook's header
/// row does not contain every required column. Carries the human-readable names
/// of the columns that could not be located, for logging.
/// </summary>
public sealed class ErpCustomerImportHeaderException : Exception
{
    public IReadOnlyList<string> MissingColumns { get; }

    public ErpCustomerImportHeaderException(IReadOnlyList<string> missingColumns)
        : base($"ERP customer import file is missing required columns: {string.Join(", ", missingColumns)}")
    {
        MissingColumns = missingColumns;
    }
}
