namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>A single row that failed ERP-customer import validation.</summary>
public record ErpCustomerImportRowError(
    int RowNumber,
    string? CustomerCode,
    string Message
);
