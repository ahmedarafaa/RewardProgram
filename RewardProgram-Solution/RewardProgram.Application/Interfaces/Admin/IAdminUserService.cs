using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Users;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminUserService
{
    // Add User (admin can only create internal staff — ShopOwner/Seller/Technician
    // register themselves via the public OTP-first flow)
    Task<Result<AdminAddUserResponse>> AddSalesManAsync(AdminAddSalesManRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> AddZoneManagerAsync(AdminAddZoneManagerRequest request, string adminUserId, CancellationToken ct = default);

    // List Users
    Task<Result<PaginatedResult<AdminUserListItemResponse>>> ListUsersAsync(AdminUserListQuery query, CancellationToken ct = default);

    // Get user details (single user, full info — used by Edit form, restore confirm dialog, etc.)
    Task<Result<AdminUserDetailResponse>> GetUserByIdAsync(string userId, CancellationToken ct = default);

    // Toggle Status
    Task<Result<AdminToggleStatusResponse>> ToggleStatusAsync(string userId, string adminUserId, CancellationToken ct = default);

    // Edit User (mirrors Add — admin only edits staff identity. ShopOwner/Seller/
    // Technician update their own profile via /api/profile endpoints.)
    Task<Result<AdminAddUserResponse>> EditSalesManAsync(string userId, AdminEditSalesManRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminAddUserResponse>> EditZoneManagerAsync(string userId, AdminEditZoneManagerRequest request, string adminUserId, CancellationToken ct = default);

    // Reassign
    Task<Result> ReassignCitiesAsync(AdminReassignCitiesRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result> ReassignRegionAsync(AdminReassignRegionRequest request, string adminUserId, CancellationToken ct = default);

    // Delete SM/ZM
    Task<Result> DeleteSalesManAsync(string userId, AdminDeleteSalesManRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result> DeleteZoneManagerAsync(string userId, AdminDeleteZoneManagerRequest request, string adminUserId, CancellationToken ct = default);

    // Restore deleted account
    Task<Result> RestoreUserAsync(string userId, string adminUserId, CancellationToken ct = default);
}
