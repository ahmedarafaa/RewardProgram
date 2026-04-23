using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Redemption;

namespace RewardProgram.Application.Interfaces;

public interface IRedemptionService
{
    Task<Result<RedemptionRequestResponse>> CreateRequestAsync(CreateRedemptionRequest request, string userId, CancellationToken ct = default);
    Task<Result<RedemptionRequestResponse?>> GetActiveRequestAsync(string userId, CancellationToken ct = default);
    Task<Result<PaginatedResult<RedemptionRequestResponse>>> GetHistoryAsync(string userId, RedemptionListQuery query, CancellationToken ct = default);
    Task<Result<AvailableBalanceResponse>> GetAvailableBalanceAsync(string userId, CancellationToken ct = default);
    Task<Result> ResendCashOtpAsync(string userId, CancellationToken ct = default);
}
