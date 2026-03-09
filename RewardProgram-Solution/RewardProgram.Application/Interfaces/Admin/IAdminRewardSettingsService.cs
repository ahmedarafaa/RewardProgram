using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.RewardSettings;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminRewardSettingsService
{
    Task<Result<RewardSettingsResponse>> GetSettingsAsync(CancellationToken ct = default);
    Task<Result<RewardSettingsResponse>> UpdateSettingsAsync(UpdateRewardSettingsRequest request, string adminUserId, CancellationToken ct = default);
}
