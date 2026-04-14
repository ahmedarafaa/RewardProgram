using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Redemption;

namespace RewardProgram.Application.Interfaces;

public interface IRedemptionApprovalService
{
    Task<Result<PaginatedResult<PendingRedemptionResponse>>> GetPendingAsync(string approverId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result<PaginatedResult<RedemptionListItem>>> GetListAsync(string approverId, RedemptionListStatusFilter status, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<Result> ApproveAsync(ApproveRedemptionRequest request, string approverId, CancellationToken ct = default);
    Task<Result> RejectAsync(RejectRedemptionRequest request, string approverId, CancellationToken ct = default);
    Task<Result> ConfirmCashHandoverAsync(ConfirmCashHandoverRequest request, string handoverById, CancellationToken ct = default);
}
