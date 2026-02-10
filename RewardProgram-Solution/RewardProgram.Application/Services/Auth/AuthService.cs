using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
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
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IOtpService otpService,
        IFileStorageService fileStorageService,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _otpService = otpService;
        _fileStorageService = fileStorageService;
        _tokenService = tokenService;
        _logger = logger;
    }

    #region Registration

    public async Task<Result<SendOtpResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request, CancellationToken ct = default)
    {
        // 1. Validate unique constraints (sequential — DbContext is not thread-safe)
        var uniqueValidation = await ValidateShopOwnerUniqueFieldsAsync(request, ct);
        if (uniqueValidation.IsFailure)
            return Result.Failure<SendOtpResponse>(uniqueValidation.Error);

        // 2. Validate district (includes city validation in one query)
        var district = await _context.Districts
            .FirstOrDefaultAsync(d =>
                d.Id == request.DistrictId &&
                d.CityId == request.CityId &&
                d.IsActive, ct);

        if (district == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.DistrictNotFound);

        if (string.IsNullOrEmpty(district.ApprovalSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 3. Upload shop image
        var imageResult = await _fileStorageService.UploadAsync(request.ShopImage, "shops", ct);
        if (imageResult.IsFailure)
            return Result.Failure<SendOtpResponse>(AuthErrors.ImageUploadFailed);

        // 4. Prepare registration data
        var registrationData = new ShopOwnerRegistrationData(
            UserType: UserType.ShopOwner,
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
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson, ct);
        if (otpResult.IsFailure)
        {
            // Cleanup uploaded image if OTP fails
            await _fileStorageService.DeleteAsync(imageResult.Value);
            return Result.Failure<SendOtpResponse>(otpResult.Error);
        }

        _logger.LogInformation(
            "OTP sent for ShopOwner registration. Mobile: {Mobile}, District: {District}",
            MobileNumberHelper.Mask(request.MobileNumber),
            district.NameAr);

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MobileNumberHelper.Mask(request.MobileNumber)
        ));
    }

    public async Task<Result<SendOtpResponse>> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken ct = default)
    {
        // 1. Validate mobile uniqueness
        var mobileExists = await _userRepository.MobileExistsAsync(request.MobileNumber, ct);
        if (mobileExists)
            return Result.Failure<SendOtpResponse>(AuthErrors.MobileAlreadyRegistered);

        // 2. Find ShopOwner by ShopCode — must be Approved
        var shopOwnerProfile = await _context.ShopOwnerProfiles
            .Include(p => p.User)
                .ThenInclude(u => u.NationalAddress)
            .FirstOrDefaultAsync(p => p.ShopCode == request.ShopCode, ct);

        if (shopOwnerProfile == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.InvalidShopCode);

        if (shopOwnerProfile.User.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<SendOtpResponse>(AuthErrors.ShopOwnerNotApproved);

        var shopOwnerUser = shopOwnerProfile.User;
        var address = shopOwnerUser.NationalAddress;

        if (string.IsNullOrEmpty(shopOwnerUser.AssignedSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 3. Serialize registration data
        var registrationData = new SellerRegistrationData(
            UserType: UserType.Seller,
            Name: request.Name,
            MobileNumber: request.MobileNumber,
            ShopCode: request.ShopCode,
            ShopOwnerId: shopOwnerProfile.UserId,
            AssignedSalesManId: shopOwnerUser.AssignedSalesManId,
            CityId: address?.CityId ?? string.Empty,
            DistrictId: address?.DistrictId ?? string.Empty,
            Street: address?.Street ?? string.Empty,
            BuildingNumber: address?.BuildingNumber ?? 0,
            PostalCode: address?.PostalCode ?? string.Empty,
            SubNumber: address?.SubNumber ?? 0
        );

        var registrationJson = JsonSerializer.Serialize(registrationData);

        // 4. Send OTP
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson, ct);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "OTP sent for Seller registration. Mobile: {Mobile}, ShopCode: {ShopCode}",
            MobileNumberHelper.Mask(request.MobileNumber),
            request.ShopCode);

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MobileNumberHelper.Mask(request.MobileNumber)
        ));
    }

    public async Task<Result<SendOtpResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request, CancellationToken ct = default)
    {
        // 1. Validate mobile uniqueness
        var mobileExists = await _userRepository.MobileExistsAsync(request.MobileNumber, ct);
        if (mobileExists)
            return Result.Failure<SendOtpResponse>(AuthErrors.MobileAlreadyRegistered);

        // 2. Validate district (exists, active, belongs to CityId, has ApprovalSalesManId)
        var district = await _context.Districts
            .FirstOrDefaultAsync(d =>
                d.Id == request.DistrictId &&
                d.CityId == request.CityId &&
                d.IsActive, ct);

        if (district == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.DistrictNotFound);

        if (string.IsNullOrEmpty(district.ApprovalSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 3. Serialize registration data
        var registrationData = new TechnicianRegistrationData(
            UserType: UserType.Technician,
            Name: request.Name,
            MobileNumber: request.MobileNumber,
            CityId: request.CityId,
            DistrictId: request.DistrictId,
            PostalCode: request.PostalCode,
            AssignedSalesManId: district.ApprovalSalesManId
        );

        var registrationJson = JsonSerializer.Serialize(registrationData);

        // 4. Send OTP
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson, ct);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "OTP sent for Technician registration. Mobile: {Mobile}, District: {District}",
            MobileNumberHelper.Mask(request.MobileNumber),
            district.NameAr);

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MobileNumberHelper.Mask(request.MobileNumber)
        ));
    }

    public async Task<Result<RegisterResponse>> VerifyRegistrationAsync(VerifyOtpRequest request, CancellationToken ct = default)
    {
        // 1. Verify OTP and get registration data
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp, ct);
        if (verifyResult.IsFailure)
            return Result.Failure<RegisterResponse>(verifyResult.Error);

        var registrationJson = verifyResult.Value.RegistrationData;
        if (string.IsNullOrEmpty(registrationJson))
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // 2. Peek at UserType to determine which creation flow to use
        var baseData = JsonSerializer.Deserialize<RegistrationDataBase>(registrationJson);
        if (baseData == null)
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        return baseData.UserType switch
        {
            UserType.ShopOwner => await CreateShopOwnerAsync(registrationJson, ct),
            UserType.Seller => await CreateSellerAsync(registrationJson, ct),
            UserType.Technician => await CreateTechnicianAsync(registrationJson, ct),
            _ => Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound)
        };
    }

    #endregion

    #region Login

    public async Task<Result<SendOtpResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByMobileAsync(request.MobileNumber, ct);

        if (user == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserNotFound);

        if (user.IsDisabled)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserDisabled);

        var otpResult = await _otpService.SendAsync(request.MobileNumber, ct: ct);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "OTP sent for login. UserId: {UserId}, Mobile: {Mobile}",
            user.Id,
            MobileNumberHelper.Mask(request.MobileNumber));

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MobileNumberHelper.Mask(request.MobileNumber)
        ));
    }

    public async Task<Result<AuthResponse>> VerifyLoginAsync(LoginVerifyRequest request, CancellationToken ct = default)
    {
        // 1. Verify OTP (returns mobile number — eliminates duplicate query)
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp, ct);
        if (verifyResult.IsFailure)
            return Result.Failure<AuthResponse>(verifyResult.Error);

        // 2. Find user by mobile number from OTP result
        var user = await _userRepository.FindByMobileAsync(verifyResult.Value.MobileNumber, ct);

        if (user == null)
            return Result.Failure<AuthResponse>(AuthErrors.UserNotFound);

        if (user.IsDisabled)
            return Result.Failure<AuthResponse>(AuthErrors.UserDisabled);

        // 3. Generate auth response (tokens + user info)
        var authResponse = await _tokenService.GenerateAuthResponseAsync(user);

        _logger.LogInformation("User logged in successfully. UserId: {UserId}", user.Id);

        return Result.Success(authResponse);
    }

    #endregion

    #region Token Management

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByRefreshTokenAsync(refreshToken, ct);

        if (user == null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        var token = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken);

        if (token == null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        if (token.RevokedOn != null)
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenRevoked);

        if (token.IsExpired)
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenExpired);

        if (user.IsDisabled)
            return Result.Failure<AuthResponse>(AuthErrors.UserDisabled);

        // Revoke old token and persist
        token.RevokedOn = DateTime.UtcNow;

        // Clean up expired/revoked tokens to prevent unbounded growth
        user.RefreshTokens.RemoveAll(t => t.Token != refreshToken && (!t.IsActive || t.IsExpired));

        await _userRepository.UpdateAsync(user);

        // Generate new auth response
        var authResponse = await _tokenService.GenerateAuthResponseAsync(user);

        _logger.LogInformation("Token refreshed for UserId: {UserId}", user.Id);

        return Result.Success(authResponse);
    }

    public async Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByRefreshTokenAsync(refreshToken, ct);

        if (user == null)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        var token = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken);

        if (token == null)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        if (!token.IsActive)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        token.RevokedOn = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Token revoked for UserId: {UserId}", user.Id);

        return Result.Success();
    }

    #endregion

    #region Private — User Creation Methods

    private async Task<Result<RegisterResponse>> CreateShopOwnerAsync(string registrationJson, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<ShopOwnerRegistrationData>(registrationJson);
        if (data == null)
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // Re-validate unique constraints (race condition protection — sequential for DbContext safety)
        if (await _userRepository.MobileExistsAsync(data.MobileNumber, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        if (await _context.ShopOwnerProfiles.AnyAsync(p => p.VAT == data.VAT, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.VatAlreadyExists);

        if (await _context.ShopOwnerProfiles.AnyAsync(p => p.CRN == data.CRN, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.CrnAlreadyExists);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
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

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create ShopOwner user: {Errors}", errors);
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.ShopOwner);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add ShopOwner role to user {UserId}: {Errors}", user.Id, errors);
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            var profile = new ShopOwnerProfile
            {
                UserId = user.Id,
                StoreName = data.StoreName,
                VAT = data.VAT,
                CRN = data.CRN,
                ShopImageUrl = data.ShopImageUrl,
                CreatedBy = user.Id
            };

            await _context.ShopOwnerProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "ShopOwner registered successfully. UserId: {UserId}, Mobile: {Mobile}",
                user.Id,
                MobileNumberHelper.Mask(data.MobileNumber));

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to complete ShopOwner registration for mobile: {Mobile}",
                MobileNumberHelper.Mask(data.MobileNumber));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    private async Task<Result<RegisterResponse>> CreateSellerAsync(string registrationJson, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<SellerRegistrationData>(registrationJson);
        if (data == null)
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // Re-validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(data.MobileNumber, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // Re-validate ShopOwner still approved
        var shopOwnerProfile = await _context.ShopOwnerProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == data.ShopOwnerId, ct);

        if (shopOwnerProfile == null)
            return Result.Failure<RegisterResponse>(AuthErrors.ShopOwnerNotFound);

        if (shopOwnerProfile.User.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<RegisterResponse>(AuthErrors.ShopOwnerNotApproved);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = data.MobileNumber,
                PhoneNumber = data.MobileNumber,
                Name = data.Name,
                MobileNumber = data.MobileNumber,
                UserType = UserType.Seller,
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

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create Seller user: {Errors}", errors);
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.Seller);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add Seller role to user {UserId}: {Errors}", user.Id, errors);
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            var profile = new SellerProfile
            {
                UserId = user.Id,
                ShopOwnerId = data.ShopOwnerId,
                CreatedBy = user.Id
            };

            await _context.SellerProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Seller registered successfully. UserId: {UserId}, Mobile: {Mobile}, ShopCode: {ShopCode}",
                user.Id,
                MobileNumberHelper.Mask(data.MobileNumber),
                data.ShopCode);

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to complete Seller registration for mobile: {Mobile}",
                MobileNumberHelper.Mask(data.MobileNumber));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    private async Task<Result<RegisterResponse>> CreateTechnicianAsync(string registrationJson, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<TechnicianRegistrationData>(registrationJson);
        if (data == null)
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // Re-validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(data.MobileNumber, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // Re-validate district still valid
        var district = await _context.Districts
            .FirstOrDefaultAsync(d =>
                d.Id == data.DistrictId &&
                d.CityId == data.CityId &&
                d.IsActive &&
                !string.IsNullOrEmpty(d.ApprovalSalesManId), ct);

        if (district == null)
            return Result.Failure<RegisterResponse>(AuthErrors.DistrictNotFound);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = data.MobileNumber,
                PhoneNumber = data.MobileNumber,
                Name = data.Name,
                MobileNumber = data.MobileNumber,
                UserType = UserType.Technician,
                RegistrationStatus = RegistrationStatus.PendingSalesman,
                AssignedSalesManId = data.AssignedSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = data.CityId,
                    DistrictId = data.DistrictId,
                    PostalCode = data.PostalCode
                }
            };

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create Technician user: {Errors}", errors);
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.Technician);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add Technician role to user {UserId}: {Errors}", user.Id, errors);
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
            }

            var profile = new TechnicianProfile
            {
                UserId = user.Id,
                CreatedBy = user.Id
            };

            await _context.TechnicianProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Technician registered successfully. UserId: {UserId}, Mobile: {Mobile}",
                user.Id,
                MobileNumberHelper.Mask(data.MobileNumber));

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to complete Technician registration for mobile: {Mobile}",
                MobileNumberHelper.Mask(data.MobileNumber));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<Result> ValidateShopOwnerUniqueFieldsAsync(RegisterShopOwnerRequest request, CancellationToken ct = default)
    {
        // Sequential — DbContext is NOT thread-safe (P5 fix)
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure(AuthErrors.MobileAlreadyRegistered);

        if (await _context.ShopOwnerProfiles.AnyAsync(p => p.VAT == request.VAT, ct))
            return Result.Failure(AuthErrors.VatAlreadyExists);

        if (await _context.ShopOwnerProfiles.AnyAsync(p => p.CRN == request.CRN, ct))
            return Result.Failure(AuthErrors.CrnAlreadyExists);

        return Result.Success();
    }

    #endregion
}
