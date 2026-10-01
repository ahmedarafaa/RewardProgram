namespace RewardProgram.Application.Contracts.Admin.Imports;

/// <summary>
/// Size limits the .xlsx importers enforce. Shared by the upload endpoints, the
/// services and the downloadable templates' instructions sheets, so the numbers a
/// user reads are the numbers actually enforced.
/// </summary>
public static class ImportLimits
{
    /// <summary>
    /// 10 MB upper bound on an uploaded workbook — well above any realistic catalogue
    /// or customer list, while still rejecting accidental large files early.
    /// </summary>
    public const long MaxFileBytes = 10 * 1024 * 1024;

    public const int MaxFileMegabytes = (int)(MaxFileBytes / (1024 * 1024));

    /// <summary>Products total ~1,100 — a generous ceiling that still bounds one import.</summary>
    public const int MaxProductRows = 10_000;

    /// <summary>ERP customer lists are longer than the product catalogue.</summary>
    public const int MaxErpCustomerRows = 20_000;
}
