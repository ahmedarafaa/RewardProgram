using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Redemptions;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminRedemptionService
{
    Task<Result<PaginatedResult<AdminRedemptionListItemResponse>>> GetAllAsync(AdminRedemptionListQuery query, CancellationToken ct = default);
    Task<Result<AdminRedemptionResponse>> GetByIdAsync(string id, CancellationToken ct = default);
}
