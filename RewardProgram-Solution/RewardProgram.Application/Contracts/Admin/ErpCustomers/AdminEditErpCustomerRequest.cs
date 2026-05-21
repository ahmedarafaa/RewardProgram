namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>
/// Edit payload for an ERP customer. CustomerCode is intentionally absent — it is
/// the ERP key referenced by ShopData / profile foreign keys and is immutable.
/// </summary>
public record AdminEditErpCustomerRequest(
    string CustomerName
);
