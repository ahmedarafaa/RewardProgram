using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Users;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminUserService
{
    // Add User
    Task<Result<AdminAddUserResponse>> AddSalesManAsync(AdminAddSalesManRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddZoneManagerAsync(AdminAddZoneManagerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddShopOwnerAsync(AdminAddShopOwnerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddSellerAsync(AdminAddSellerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddTechnicianAsync(AdminAddTechnicianRequest request, string adminUserId, CancellationToken ct = default);

    // List Users
    Task<Result<PaginatedResult<AdminUserListItemResponse>>> ListUsersAsync(AdminUserListQuery query, CancellationToken ct = default);

    // Toggle Status
    Task<Result<AdminToggleStatusResponse>> ToggleStatusAsync(string userId, string adminUserId, CancellationToken ct = default);

    // Edit User
    Task<Result<AdminAddUserResponse>> EditSalesManAsync(string userId, AdminEditSalesManRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> EditZoneManagerAsync(string userId, AdminEditZoneManagerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> EditShopOwnerAsync(string userId, AdminEditShopOwnerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> EditSellerAsync(string userId, AdminEditSellerRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> EditTechnicianAsync(string userId, AdminEditTechnicianRequest request, string adminUserId, CancellationToken ct = default);
}
