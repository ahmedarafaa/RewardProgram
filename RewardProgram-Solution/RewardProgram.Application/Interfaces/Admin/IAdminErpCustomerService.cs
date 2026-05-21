using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminErpCustomerService
{
    Task<Result<AdminErpCustomerResponse>> AddErpCustomerAsync(AdminAddErpCustomerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminErpCustomerResponse>> EditErpCustomerAsync(string erpCustomerId, AdminEditErpCustomerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result> DeleteErpCustomerAsync(string erpCustomerId, string adminUserId, CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminErpCustomerResponse>>> ListErpCustomersAsync(AdminErpCustomerListQuery query, CancellationToken ct = default);
    Task<Result<List<AdminErpCustomerResponse>>> ExportErpCustomersAsync(AdminErpCustomerListQuery query, CancellationToken ct = default);
    Task<Result<AdminErpCustomerResponse>> GetErpCustomerAsync(string erpCustomerId, CancellationToken ct = default);
    Task<Result<ErpCustomerImportResultResponse>> ImportErpCustomersAsync(Stream xlsxStream, string adminUserId, CancellationToken ct = default);
}
