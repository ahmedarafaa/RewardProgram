namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>
/// One raw row parsed from an ERP-customer import .xlsx file. Cell values are kept
/// as strings so the service can validate them and report precise per-row errors.
/// </summary>
public record ErpCustomerImportRow(
    int RowNumber,
    string? CustomerCode,
    string? CustomerName
);
