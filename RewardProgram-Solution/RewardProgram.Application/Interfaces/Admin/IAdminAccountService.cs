using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Accounts;

namespace RewardProgram.Application.Interfaces.Admin;

/// <summary>
/// Management of admin-dashboard accounts and their permissions. SystemAdmin-only —
/// the controller enforces the role, and SystemAdmin accounts themselves are
/// read-only here (they cannot be modified or deleted).
/// </summary>
public interface IAdminAccountService
{
    Task<Result<List<AdminAccountListItem>>> ListAsync(CancellationToken ct = default);
    Task<Result<AdminAccountDetailResponse>> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Result<AdminAccountDetailResponse>> CreateAsync(CreateAdminAccountRequest request, CancellationToken ct = default);
    Task<Result<AdminAccountDetailResponse>> UpdateAsync(string id, UpdateAdminAccountRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(string id, CancellationToken ct = default);
    Task<Result<AdminAccountDetailResponse>> SetPermissionsAsync(string id, SetAdminPermissionsRequest request, CancellationToken ct = default);
    Result<List<PermissionCatalogModule>> GetPermissionCatalog();
}
