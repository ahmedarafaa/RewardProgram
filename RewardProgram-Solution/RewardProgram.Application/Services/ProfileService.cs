using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Profile;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IFileStorageService fileStorage,
        ILogger<ProfileService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<Result<ProfileResponse>> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<ProfileResponse>(AuthErrors.UserNotFound);

        var isStaff = user.UserType is Domain.Enums.UserEnums.UserType.SalesMan
            or Domain.Enums.UserEnums.UserType.ZoneManager;

        decimal? points = null;
        if (!isStaff)
        {
            var wallet = await _context.Wallets
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.UserId == userId, ct);
            points = wallet?.Balance ?? 0;
        }

        string? cityName = null;
        if (user.NationalAddress is not null)
        {
            cityName = await _context.Cities
                .AsNoTracking()
                .Where(c => c.Id == user.NationalAddress.CityId)
                .Select(c => c.NameAr)
                .FirstOrDefaultAsync(ct);
        }

        return Result.Success(new ProfileResponse(
            user.Id,
            user.Name,
            user.MobileNumber,
            user.UserType,
            user.ProfileImageUrl,
            points,
            cityName,
            user.NationalAddress?.District,
            user.NationalAddress?.Street,
            user.NationalAddress?.BuildingNumber,
            user.NationalAddress?.PostalCode,
            user.NationalAddress?.SubNumber
        ));
    }

    public async Task<Result<string>> UpdateProfilePhotoAsync(string userId, IFormFile photo, CancellationToken ct = default)
    {
        var validation = ImageUploadValidator.Validate(photo);
        if (validation.IsFailure)
            return Result.Failure<string>(validation.Error);

        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<string>(AuthErrors.UserNotFound);

        var oldPhotoUrl = user.ProfileImageUrl;

        var uploadResult = await _fileStorage.UploadAsync(
            photo.OpenReadStream(),
            photo.FileName,
            "profiles",
            ct);

        if (uploadResult.IsFailure)
            return Result.Failure<string>(uploadResult.Error);

        user.ProfileImageUrl = uploadResult.Value;
        await _userRepository.UpdateAsync(user);

        // Delete old photo only after the new one is committed to user record,
        // so an upload failure doesn't lose the previous photo.
        if (!string.IsNullOrEmpty(oldPhotoUrl))
            await _fileStorage.DeleteAsync(oldPhotoUrl);

        _logger.LogInformation("Profile photo updated for user {UserId}", userId);

        return Result.Success(uploadResult.Value);
    }

    public async Task<Result> DeleteAccountAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        // Check for pending redemption requests
        var hasPendingRedemptions = await _context.RedemptionRequests
            .AnyAsync(r => r.UserId == userId
                && r.Status != Domain.Enums.RedemptionRequestStatus.Completed
                && r.Status != Domain.Enums.RedemptionRequestStatus.Rejected
                && r.Status != Domain.Enums.RedemptionRequestStatus.Cancelled, ct);

        if (hasPendingRedemptions)
            return Result.Failure(ProfileErrors.HasPendingRedemptions);

        // Disable account and mark as deleted
        user.IsDisabled = true;
        user.IsAccountDeleted = true;
        user.AccountDeletedAt = DateTime.UtcNow;

        // Revoke all refresh tokens + clear FCM token so the deleted account
        // stops receiving push notifications immediately.
        foreach (var token in user.RefreshTokens.Where(t => t.RevokedOn == null))
            token.RevokedOn = DateTime.UtcNow;
        user.FcmToken = null;

        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Account deleted (disabled) for user {UserId}", userId);

        return Result.Success();
    }
}
