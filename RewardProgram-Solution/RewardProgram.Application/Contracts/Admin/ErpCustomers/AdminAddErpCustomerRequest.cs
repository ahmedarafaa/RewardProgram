namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

public record AdminAddErpCustomerRequest(
    string CustomerCode,
    string CustomerName,
    string? ShortAddress = null
);
