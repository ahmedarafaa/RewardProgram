namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

/// <summary>
/// ERP customer as returned by the admin endpoints. <see cref="HasShopData"/> and
/// <see cref="LinkedUserCount"/> tell the dashboard whether the row can be deleted.
/// </summary>
public record AdminErpCustomerResponse(
    string Id,
    string CustomerCode,
    string CustomerName,
    string? ShortAddress,
    bool HasShopData,
    int LinkedUserCount
);
