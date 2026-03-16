using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Invitation;

namespace RewardProgram.Application.Interfaces;

public interface IInvitationService
{
    Task<Result<InvitationInfoResponse>> GetInvitationInfoAsync(string userId, CancellationToken ct = default);
    Task CreditInvitationRewardsAsync(string invitedUserId, CancellationToken ct = default);
}
