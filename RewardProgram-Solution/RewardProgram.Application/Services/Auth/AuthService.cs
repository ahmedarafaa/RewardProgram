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

    #region Registration — OTP-First Flow

    public async Task<Result<SendOtpResponse>> SendRegistrationOtpAsync(SendOtpRequest request, CancellationToken ct = default)
    {
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        // Validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<SendOtpResponse>(AuthErrors.MobileAlreadyRegistered);

        // Send OTP without registration data
        var otpResult = await _otpService.SendAsync(mobile, ct: ct);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "Registration OTP sent. Mobile: {Mobile}",
            MobileNumberHelper.Mask(mobile));

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MobileNumberHelper.Mask(mobile)
        ));
    }

    public async Task<Result<RegisterResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request, CancellationToken ct = default)
    {
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        // 1. Validate all fields BEFORE consuming OTP (so user can retry without re-requesting OTP)

        // Validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // Validate CustomerCode exists in ErpCustomers
        var erpCustomer = await _context.ErpCustomers
            .FirstOrDefaultAsync(e => e.CustomerCode == request.CustomerCode, ct);

        if (erpCustomer == null)
            return Result.Failure<RegisterResponse>(AuthErrors.CustomerCodeNotFound);

        // Validate city
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive, ct);

        if (city == null)
            return Result.Failure<RegisterResponse>(AuthErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<RegisterResponse>(AuthErrors.NoApprovalSalesMan);

        // ShopOwner ALWAYS provides shop data (owner always wins — overwrites Seller's data)
        if (string.IsNullOrEmpty(request.StoreName) || string.IsNullOrEmpty(request.VAT)
            || string.IsNullOrEmpty(request.CRN) || request.ShopImage == null
            || request.NationalAddress == null || string.IsNullOrEmpty(request.ShortAddress))
            return Result.Failure<RegisterResponse>(AuthErrors.ShopDataRequired);

        // Reject if another ShopOwner already owns this CustomerCode
        var existingOwner = await _context.ShopOwnerProfiles
            .AnyAsync(p => p.CustomerCode == request.CustomerCode, ct);
        if (existingOwner)
            return Result.Failure<RegisterResponse>(AuthErrors.CustomerCodeAlreadyOwned);

        var existingShopData = await _context.ShopData
            .FirstOrDefaultAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

        // Validate VAT/CRN/ShortAddress uniqueness (exclude own record when overwriting)
        var uniqueValidation = await ShopDataValidationHelper.ValidateUniqueFieldsAsync(
            _context, request.VAT, request.CRN, request.ShortAddress, existingShopData?.CustomerCode, ct);
        if (uniqueValidation.IsFailure)
            return Result.Failure<RegisterResponse>(uniqueValidation.Error);

        // Upload shop image
        var imageUploadResult = await FileUploadHelper.UploadShopImageAsync(request.ShopImage, _fileStorageService, ct);
        if (imageUploadResult.IsFailure)
            return Result.Failure<RegisterResponse>(imageUploadResult.Error);

        var shopImageUrl = imageUploadResult.Value;

        // 2. All validation passed — NOW consume OTP
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp, ct);
        if (verifyResult.IsFailure)
            return Result.Failure<RegisterResponse>(verifyResult.Error);

        if (verifyResult.Value.MobileNumber != mobile)
            return Result.Failure<RegisterResponse>(AuthErrors.MobileMismatch);

        // 3. Re-check mobile uniqueness (race condition protection after OTP consumed)
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // 4. Create user in transaction
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.OwnerName,
                MobileNumber = mobile,
                UserType = UserType.ShopOwner,
                RegistrationStatus = RegistrationStatus.PendingSalesman,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = request.CityId,
                    Street = request.NationalAddress?.Street ?? string.Empty,
                    BuildingNumber = request.NationalAddress?.BuildingNumber ?? 0,
                    PostalCode = request.NationalAddress?.PostalCode ?? string.Empty,
                    SubNumber = request.NationalAddress?.SubNumber ?? 0,
                    District = request.NationalAddress?.District ?? string.Empty
                }
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.ShopOwner, AuthErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(createRoleResult.Error);
            }

            // ShopOwner always owns ShopData — create if missing, overwrite if exists
            if (existingShopData == null)
            {
                var shopData = new ShopData
                {
                    CustomerCode = request.CustomerCode,
                    StoreName = request.StoreName!,
                    VAT = request.VAT!,
                    CRN = request.CRN!,
                    ShopImageUrl = shopImageUrl,
                    ShortAddress = request.ShortAddress!,
                    District = request.NationalAddress!.District ?? string.Empty,
                    CityId = request.CityId,
                    Street = request.NationalAddress.Street,
                    BuildingNumber = request.NationalAddress.BuildingNumber,
                    PostalCode = request.NationalAddress.PostalCode,
                    SubNumber = request.NationalAddress.SubNumber,
                    EnteredByUserId = user.Id,
                    CreatedBy = user.Id
                };
                await _context.ShopData.AddAsync(shopData, ct);
            }
            else
            {
                // ShopData was created by a Seller — ShopOwner overwrites it
                existingShopData.StoreName = request.StoreName!;
                existingShopData.VAT = request.VAT!;
                existingShopData.CRN = request.CRN!;
                existingShopData.ShopImageUrl = shopImageUrl;
                existingShopData.ShortAddress = request.ShortAddress!;
                existingShopData.District = request.NationalAddress!.District ?? string.Empty;
                existingShopData.CityId = request.CityId;
                existingShopData.Street = request.NationalAddress.Street;
                existingShopData.BuildingNumber = request.NationalAddress.BuildingNumber;
                existingShopData.PostalCode = request.NationalAddress.PostalCode;
                existingShopData.SubNumber = request.NationalAddress.SubNumber;
                existingShopData.EnteredByUserId = user.Id;
                existingShopData.UpdatedBy = user.Id;
                existingShopData.UpdatedAt = DateTime.UtcNow;
            }

            var profile = new ShopOwnerProfile
            {
                UserId = user.Id,
                CustomerCode = request.CustomerCode,
                CreatedBy = user.Id
            };

            await _context.ShopOwnerProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "ShopOwner registered successfully. UserId: {UserId}, Mobile: {Mobile}",
                user.Id,
                MobileNumberHelper.Mask(mobile));

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to complete ShopOwner registration for mobile: {Mobile}",
                MobileNumberHelper.Mask(mobile));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    public async Task<Result<RegisterResponse>> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken ct = default)
    {
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        // 1. Validate all fields BEFORE consuming OTP

        // Validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // Validate CustomerCode exists in ErpCustomers
        var erpCustomer = await _context.ErpCustomers
            .FirstOrDefaultAsync(e => e.CustomerCode == request.CustomerCode, ct);

        if (erpCustomer == null)
            return Result.Failure<RegisterResponse>(AuthErrors.CustomerCodeNotFound);

        // Check if ShopData already exists for this CustomerCode
        var existingShopData = await _context.ShopData
            .FirstOrDefaultAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

        var shopDataExists = existingShopData != null;
        string? shopImageUrl = null;
        string cityId;

        if (!shopDataExists)
        {
            // Shop data required
            if (string.IsNullOrEmpty(request.StoreName) || string.IsNullOrEmpty(request.VAT)
                || string.IsNullOrEmpty(request.CRN) || request.ShopImage == null
                || string.IsNullOrEmpty(request.CityId) || request.NationalAddress == null
                || string.IsNullOrEmpty(request.ShortAddress))
                return Result.Failure<RegisterResponse>(AuthErrors.ShopDataRequired);

            // Validate VAT/CRN/ShortAddress uniqueness
            var uniqueValidation = await ShopDataValidationHelper.ValidateUniqueFieldsAsync(
                _context, request.VAT, request.CRN, request.ShortAddress, ct: ct);
            if (uniqueValidation.IsFailure)
                return Result.Failure<RegisterResponse>(uniqueValidation.Error);

            // Upload shop image
            var imageUploadResult = await FileUploadHelper.UploadShopImageAsync(request.ShopImage, _fileStorageService, ct);
            if (imageUploadResult.IsFailure)
                return Result.Failure<RegisterResponse>(imageUploadResult.Error);

            shopImageUrl = imageUploadResult.Value;
            cityId = request.CityId;
        }
        else
        {
            // Use ShopData's city
            cityId = existingShopData!.CityId;
        }

        // Validate city and get SalesMan
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == cityId && c.IsActive, ct);

        if (city == null)
            return Result.Failure<RegisterResponse>(AuthErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<RegisterResponse>(AuthErrors.NoApprovalSalesMan);

        // 2. All validation passed — NOW consume OTP
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp, ct);
        if (verifyResult.IsFailure)
            return Result.Failure<RegisterResponse>(verifyResult.Error);

        if (verifyResult.Value.MobileNumber != mobile)
            return Result.Failure<RegisterResponse>(AuthErrors.MobileMismatch);

        // 3. Re-check mobile uniqueness (race condition protection after OTP consumed)
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // 4. Create user in transaction
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            // Determine address from ShopData or from registration data
            string street = shopDataExists ? existingShopData!.Street : request.NationalAddress!.Street;
            int buildingNumber = shopDataExists ? existingShopData!.BuildingNumber : request.NationalAddress!.BuildingNumber;
            string postalCode = shopDataExists ? existingShopData!.PostalCode : request.NationalAddress!.PostalCode;
            int subNumber = shopDataExists ? existingShopData!.SubNumber : request.NationalAddress!.SubNumber;
            string district = shopDataExists ? existingShopData!.District : (request.NationalAddress?.District ?? string.Empty);

            var user = new ApplicationUser
            {
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.Name,
                MobileNumber = mobile,
                UserType = UserType.Seller,
                RegistrationStatus = RegistrationStatus.PendingSalesman,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = cityId,
                    Street = street,
                    BuildingNumber = buildingNumber,
                    PostalCode = postalCode,
                    SubNumber = subNumber,
                    District = district
                }
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.Seller, AuthErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(createRoleResult.Error);
            }

            // Create ShopData if needed
            if (!shopDataExists)
            {
                var shopDataStillMissing = !await _context.ShopData
                    .AnyAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

                if (shopDataStillMissing)
                {
                    var shopData = new ShopData
                    {
                        CustomerCode = request.CustomerCode,
                        StoreName = request.StoreName!,
                        VAT = request.VAT!,
                        CRN = request.CRN!,
                        ShopImageUrl = shopImageUrl!,
                        ShortAddress = request.ShortAddress!,
                        District = request.NationalAddress?.District ?? string.Empty,
                        CityId = cityId,
                        Street = request.NationalAddress!.Street,
                        BuildingNumber = request.NationalAddress.BuildingNumber,
                        PostalCode = request.NationalAddress.PostalCode,
                        SubNumber = request.NationalAddress.SubNumber,
                        EnteredByUserId = user.Id,
                        CreatedBy = user.Id
                    };
                    await _context.ShopData.AddAsync(shopData, ct);
                }
            }

            var profile = new SellerProfile
            {
                UserId = user.Id,
                CustomerCode = request.CustomerCode,
                CreatedBy = user.Id
            };

            await _context.SellerProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Seller registered successfully. UserId: {UserId}, Mobile: {Mobile}, CustomerCode: {CustomerCode}",
                user.Id,
                MobileNumberHelper.Mask(mobile),
                request.CustomerCode);

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to complete Seller registration for mobile: {Mobile}",
                MobileNumberHelper.Mask(mobile));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    public async Task<Result<RegisterResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request, CancellationToken ct = default)
    {
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        // 1. Validate all fields BEFORE consuming OTP

        // Validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // Validate city and has ApprovalSalesManId
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive, ct);

        if (city == null)
            return Result.Failure<RegisterResponse>(AuthErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<RegisterResponse>(AuthErrors.NoApprovalSalesMan);

        // 2. All validation passed — NOW consume OTP
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp, ct);
        if (verifyResult.IsFailure)
            return Result.Failure<RegisterResponse>(verifyResult.Error);

        if (verifyResult.Value.MobileNumber != mobile)
            return Result.Failure<RegisterResponse>(AuthErrors.MobileMismatch);

        // 3. Re-check mobile uniqueness (race condition protection after OTP consumed)
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // 4. Create user in transaction
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.Name,
                MobileNumber = mobile,
                UserType = UserType.Technician,
                RegistrationStatus = RegistrationStatus.PendingSalesman,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = request.CityId,
                    PostalCode = request.PostalCode,
                    District = request.District
                }
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.Technician, AuthErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<RegisterResponse>(createRoleResult.Error);
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
                MobileNumberHelper.Mask(mobile));

            return Result.Success(new RegisterResponse(
                UserId: user.Id,
                Message: "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to complete Technician registration for mobile: {Mobile}",
                MobileNumberHelper.Mask(mobile));
            return Result.Failure<RegisterResponse>(AuthErrors.CreateUserFailed);
        }
    }

    #endregion

    #region Login

    public async Task<Result<SendOtpResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);
        var user = await _userRepository.FindByMobileAsync(mobile, ct);

        if (user == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserNotFound);

        if (user.IsDisabled)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserDisabled);

        if (user.RegistrationStatus == RegistrationStatus.Rejected)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserRejected);

        if (user.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserNotApproved);

        var otpResult = await _otpService.SendAsync(mobile, ct: ct);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "OTP sent for login. UserId: {UserId}, Mobile: {Mobile}",
            user.Id,
            MobileNumberHelper.Mask(mobile));

        return Result.Success(new SendOtpResponse(
            PinId: otpResult.Value,
            MaskedMobileNumber: MobileNumberHelper.Mask(mobile)
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

        if (user.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<AuthResponse>(AuthErrors.UserNotApproved);

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

}
