using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Users;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminUserService
{
    Task<Result<AdminAddUserResponse>> AddSalesManAsync(AdminAddSalesManRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddZoneManagerAsync(AdminAddZoneManagerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddShopOwnerAsync(AdminAddShopOwnerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddSellerAsync(AdminAddSellerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddTechnicianAsync(AdminAddTechnicianRequest request, string adminUserId, CancellationToken ct = default);
}
