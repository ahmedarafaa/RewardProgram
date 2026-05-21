namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>
/// Outcome of an ERP-customer import run. Valid rows are upserted (created or
/// updated by CustomerCode); invalid rows are skipped and listed in <see cref="Errors"/>.
/// </summary>
public record ErpCustomerImportResultResponse(
    int TotalRows,
    int Created,
    int Updated,
    int Failed,
    List<ErpCustomerImportRowError> Errors
);
