using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IApprovalService
{
    Task<Result<PaginatedResult<PendingUserResponse>>> GetPendingRequestsAsync(string approverId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result> ApproveAsync(string userId, string approverId, CancellationToken ct = default);
    Task<Result> RejectAsync(string userId, string reason, string approverId, CancellationToken ct = default);
}
