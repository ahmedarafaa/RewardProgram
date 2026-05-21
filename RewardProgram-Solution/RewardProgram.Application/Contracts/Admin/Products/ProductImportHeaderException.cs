namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>
/// Thrown by <c>IProductImportReader</c> when the uploaded workbook's header row
/// does not contain every required column. Carries the human-readable names of
/// the columns that could not be located, for logging.
/// </summary>
public sealed class ProductImportHeaderException : Exception
{
    public IReadOnlyList<string> MissingColumns { get; }

    public ProductImportHeaderException(IReadOnlyList<string> missingColumns)
        : base($"Product import file is missing required columns: {string.Join(", ", missingColumns)}")
    {
        MissingColumns = missingColumns;
    }
}
