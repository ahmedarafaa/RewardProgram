using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Profile;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ProfileService> _logger;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];
    private const long MaxPhotoSize = 5 * 1024 * 1024; // 5MB

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
        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(ProfileErrors.InvalidImageType);

        if (photo.Length > MaxPhotoSize)
            return Result.Failure<string>(ProfileErrors.ImageTooLarge);

        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<string>(AuthErrors.UserNotFound);

        // Delete old photo if exists
        if (!string.IsNullOrEmpty(user.ProfileImageUrl))
            await _fileStorage.DeleteAsync(user.ProfileImageUrl);

        // Upload new photo
        var uploadResult = await _fileStorage.UploadAsync(
            photo.OpenReadStream(),
            $"{Guid.NewGuid()}{extension}",
            "profiles",
            ct);

        if (uploadResult.IsFailure)
            return Result.Failure<string>(uploadResult.Error);

        user.ProfileImageUrl = uploadResult.Value;
        await _userRepository.UpdateAsync(user);

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

        // Revoke all refresh tokens
        foreach (var token in user.RefreshTokens.Where(t => t.RevokedOn == null))
            token.RevokedOn = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Account deleted (disabled) for user {UserId}", userId);

        return Result.Success();
    }
}
