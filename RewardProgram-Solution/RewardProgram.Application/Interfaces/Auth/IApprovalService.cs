using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IApprovalService
{
    Task<Result<List<PendingUserResponse>>> GetPendingRequestsAsync(string approverId);
    Task<Result> ApproveAsync(string userId, string approverId);
    Task<Result> RejectAsync(string userId, string reason, string approverId);
}
