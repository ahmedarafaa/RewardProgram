namespace RewardProgram.Application.Contracts.Admin.ErpCustomers;

public record AdminErpCustomerListQuery(
    string? Search,
    int Page = 1,
    int PageSize = 20
);
