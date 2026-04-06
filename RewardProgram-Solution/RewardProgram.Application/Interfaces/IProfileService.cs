using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Profile;

namespace RewardProgram.Application.Interfaces;

public interface IProfileService
{
    Task<Result<ProfileResponse>> GetProfileAsync(string userId, CancellationToken ct = default);
    Task<Result<string>> UpdateProfilePhotoAsync(string userId, IFormFile photo, CancellationToken ct = default);
    Task<Result> DeleteAccountAsync(string userId, CancellationToken ct = default);
}
