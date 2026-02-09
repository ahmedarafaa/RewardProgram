using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Interfaces.Files;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using System.Text.Json;

namespace RewardProgram.Application.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpService _otpService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOtpService otpService,
        IFileStorageService fileStorageService,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userManager = userManager;
        _otpService = otpService;
        _fileStorageService = fileStorageService;
        _tokenService = tokenService;
        _logger = logger;
    }

    #region ShopOwner Registration

    public async Task<Result<SendOtpResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request)
    {
        // 1. Validate unique constraints
        var uniqueValidation = await ValidateShopOwnerUniqueFieldsAsync(request);
        if (uniqueValidation.IsFailure)
            return Result.Failure<SendOtpResponse>(uniqueValidation.Error);

        // 2. Validate district (includes city validation in one query)
        var district = await _context.Districts
            .FirstOrDefaultAsync(d =>
                d.Id == request.DistrictId &&
                d.CityId == request.CityId &&
                d.IsActive);

        if (district == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.DistrictNotFound);

        if (string.IsNullOrEmpty(district.ApprovalSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 3. Upload shop image
        var imageResult = await _fileStorageService.UploadAsync(request.ShopImage, "shops");
        if (imageResult.IsFailure)
            return Result.Failure<SendOtpResponse>(AuthErrors.ImageUploadFailed);

        // 4. Prepare registration data
        var registrationData = new ShopOwnerRegistrationData(
            StoreName: request.StoreName,
            OwnerName: request.OwnerName,
            MobileNumber: request.MobileNumber,
            VAT: request.VAT,
            CRN: request.CRN,
            ShopImageUrl: imageResult.Value,
            CityId: request.CityId,
            DistrictId: request.DistrictId,
            Zone: district.Zone,
            Street: request.NationalAddress.Street,
            BuildingNumber: request.NationalAddress.BuildingNumber,
            PostalCode: request.NationalAddress.PostalCode,
            SubNumber: request.NationalAddress.SubNumber,
            AssignedSalesManId: district.ApprovalSalesManId
        );

        var registrationJson = JsonSerializer.Serialize(registrationData);

        // 5. Send OTP
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson);
        if (otpResult.IsFailure)
        {
            // Cleanup uploaded image if OTP fails
            await _fileStorageService.DeleteAsync(imageResult.Value);
            return Result.Failure<SendOtpResponse>(otpResult.Error);
        }

        _logger.LogInformation(
            "OTP sent for ShopOwner registration. Mobile: {Mobile}, District: {District}",
            MaskMobile(request.MobileNumber),
            district.NameAr);

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MaskMobile(request.MobileNumber)
        ));
    }

    public async Task<Result<RegisterResponse>> VerifyRegistrationAsync(VerifyOtpRequest request)
    {
        // 1. Verify OTP and get registration data
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp);
        if (verifyResult.IsFailure)
            return Result.Failure<RegisterResponse>(verifyResult.Error);

        var registrationJson = verifyResult.Value;
        if (string.IsNullOrEmpty(registrationJson))
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // 2. Deserialize registration data
        var data = JsonSerializer.Deserialize<ShopOwnerRegistrationData>(registrationJson);
        if (data == null)
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // 3. Re-validate unique constraints (race condition protection)
        var mobileExists = await _userManager.Users.AnyAsync(u => u.MobileNumber == data.MobileNumber);
        if (mobileExists)
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        var vatExists = await _context.ShopOwnerProfiles.AnyAsync(p => p.VAT == data.VAT);
        if (vatExists)
            return Result.Failure<RegisterResponse>(AuthErrors.VatAlreadyExists);

        var crnExists = await _context.ShopOwnerProfiles.AnyAsync(p => p.CRN == data.CRN);
        if (crnExists)
            return Result.Failure<RegisterResponse>(AuthErrors.CrnAlreadyExists);

        // Use transaction to ensure all-or-nothing registration
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 4. Create ApplicationUser
            var user = new ApplicationUser
            {
                UserName = data.MobileNumber,
                PhoneNumber = data.MobileNumber,
                Name = data.OwnerName,
                MobileNumber = data.MobileNumber,
                UserType = UserType.ShopOwner,
                RegistrationStatus = RegistrationStatus.PendingSalesman,
                AssignedSalesManId = data.AssignedSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = data.CityId,
                    DistrictId = data.DistrictId,
                    Street = data.Street,
                    BuildingNumber = data.BuildingNumber,
                    PostalCode = data.PostalCode,
                    SubNumber = data.SubNumber
                }
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create ShopOwner user: {Errors}", errors);
                await transaction.RollbackAsync();
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            // 5. Add role - treat failure as an error
            var roleResult = await _userManager.AddToRoleAsync(user, UserRoles.ShopOwner);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add ShopOwner role to user {UserId}: {Errors}", user.Id, errors);
                await transaction.RollbackAsync();
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            // 6. Create ShopOwnerProfile
            var profile = new ShopOwnerProfile
            {
                UserId = user.Id,
                StoreName = data.StoreName,
                VAT = data.VAT,
                CRN = data.CRN,
                ShopImageUrl = data.ShopImageUrl,
                CreatedBy = user.Id
            };

            await _context.ShopOwnerProfiles.AddAsync(profile);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "ShopOwner registered successfully. UserId: {UserId}, Mobile: {Mobile}",
                user.Id,
                MaskMobile(data.MobileNumber));

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to complete ShopOwner registration for mobile: {Mobile}",
                MaskMobile(data.MobileNumber));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    #endregion

    #region Login

    public async Task<Result<SendOtpResponse>> LoginAsync(LoginRequest request)
    {
        // 1. Find user by mobile number
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.MobileNumber == request.MobileNumber);

        if (user == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserNotFound);

        if (user.IsDisabled)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserDisabled);

        // 2. Send OTP (no registration data for login)
        var otpResult = await _otpService.SendAsync(request.MobileNumber);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "OTP sent for login. UserId: {UserId}, Mobile: {Mobile}",
            user.Id,
            MaskMobile(request.MobileNumber));

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MaskMobile(request.MobileNumber)
        ));
    }

    public async Task<Result<AuthResponse>> VerifyLoginAsync(LoginVerifyRequest request)
    {
        // 1. Get OTP record to find mobile number
        var otpCode = await _context.OtpCodes
            .FirstOrDefaultAsync(o => o.PinId == request.PinId && !o.IsUsed);

        if (otpCode == null)
            return Result.Failure<AuthResponse>(AuthErrors.OtpNotFound);

        // 2. Find user by mobile number
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.MobileNumber == otpCode.MobileNumber);

        if (user == null)
            return Result.Failure<AuthResponse>(AuthErrors.UserNotFound);

        if (user.IsDisabled)
            return Result.Failure<AuthResponse>(AuthErrors.UserDisabled);

        // 3. Verify OTP
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp);
        if (verifyResult.IsFailure)
            return Result.Failure<AuthResponse>(verifyResult.Error);

        // 4. Generate auth response (tokens + user info)
        var authResponse = await _tokenService.GenerateAuthResponseAsync(user);

        _logger.LogInformation("User logged in successfully. UserId: {UserId}", user.Id);

        return Result.Success(authResponse);
    }

    #endregion

    #region Token Management

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken)
    {
        // 1. Find user with this refresh token
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken));

        if (user == null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        // 2. Validate token
        var token = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken);

        if (token == null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        if (token.RevokedOn != null)
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenRevoked);

        if (token.IsExpired)
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenExpired);

        if (user.IsDisabled)
            return Result.Failure<AuthResponse>(AuthErrors.UserDisabled);

        // 3. Revoke old token
        token.RevokedOn = DateTime.UtcNow;

        // 4. Generate new auth response
        var authResponse = await _tokenService.GenerateAuthResponseAsync(user);

        _logger.LogInformation("Token refreshed for UserId: {UserId}", user.Id);

        return Result.Success(authResponse);
    }

    public async Task<Result> RevokeTokenAsync(string refreshToken)
    {
        // 1. Find user with this refresh token
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken));

        if (user == null)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        // 2. Validate and revoke token
        var token = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken);

        if (token == null)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        if (!token.IsActive)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        token.RevokedOn = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Token revoked for UserId: {UserId}", user.Id);

        return Result.Success();
    }

    #endregion

    #region Private Helper Methods

    private async Task<Result> ValidateShopOwnerUniqueFieldsAsync(RegisterShopOwnerRequest request)
    {
        // Check mobile number
        var mobileExists = await _userManager.Users
            .AnyAsync(u => u.MobileNumber == request.MobileNumber);
        if (mobileExists)
            return Result.Failure(AuthErrors.MobileAlreadyRegistered);

        // Check VAT
        var vatExists = await _context.ShopOwnerProfiles
            .AnyAsync(p => p.VAT == request.VAT);
        if (vatExists)
            return Result.Failure(AuthErrors.VatAlreadyExists);

        // Check CRN
        var crnExists = await _context.ShopOwnerProfiles
            .AnyAsync(p => p.CRN == request.CRN);
        if (crnExists)
            return Result.Failure(AuthErrors.CrnAlreadyExists);

        return Result.Success();
    }

    private static string MaskMobile(string mobile)
    {
        if (string.IsNullOrEmpty(mobile) || mobile.Length < 4)
            return "****";

        return $"{mobile[..3]}****{mobile[^3..]}";
    }

    #endregion
}
internal record ShopOwnerRegistrationData(
    string StoreName,
    string OwnerName,
    string MobileNumber,
    string VAT,
    string CRN,
    string ShopImageUrl,
    string CityId,
    string DistrictId,
    Zone Zone,
    string Street,
    int BuildingNumber,
    string PostalCode,
    int SubNumber,
    string AssignedSalesManId
);
