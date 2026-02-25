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
        // 1. Validate mobile uniqueness
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<SendOtpResponse>(AuthErrors.MobileAlreadyRegistered);

        // 2. Validate CustomerCode exists in ErpCustomers
        var erpCustomer = await _context.ErpCustomers
            .FirstOrDefaultAsync(e => e.CustomerCode == request.CustomerCode, ct);

        if (erpCustomer == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.CustomerCodeNotFound);

        // 3. Validate city (no RegionId filter — derive region from city)
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive, ct);

        if (city == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 4. Check if ShopData already exists for this CustomerCode
        var shopDataExists = await _context.ShopData
            .AnyAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

        string? shopImageUrl = null;

        if (!shopDataExists)
        {
            // Shop data required
            if (string.IsNullOrEmpty(request.StoreName) || string.IsNullOrEmpty(request.VAT)
                || string.IsNullOrEmpty(request.CRN) || request.ShopImage == null
                || request.NationalAddress == null)
                return Result.Failure<SendOtpResponse>(AuthErrors.ShopDataRequired);

            // Validate VAT/CRN uniqueness against ShopData table
            var uniqueValidation = await ValidateUniqueFieldsAsync(request.VAT, request.CRN, ct);
            if (uniqueValidation.IsFailure)
                return Result.Failure<SendOtpResponse>(uniqueValidation.Error);

            // Upload shop image
            using var imageStream = request.ShopImage.OpenReadStream();
            var imageResult = await _fileStorageService.UploadAsync(imageStream, request.ShopImage.FileName, "shops", ct);
            if (imageResult.IsFailure)
                return Result.Failure<SendOtpResponse>(AuthErrors.ImageUploadFailed);

            shopImageUrl = imageResult.Value;
        }

        // 5. Prepare registration data
        var registrationData = new ShopOwnerRegistrationData(
            UserType: UserType.ShopOwner,
            CustomerCode: request.CustomerCode,
            OwnerName: request.OwnerName,
            MobileNumber: request.MobileNumber,
            CityId: request.CityId,
            AssignedSalesManId: city.ApprovalSalesManId,
            ShopDataAlreadyExists: shopDataExists,
            StoreName: request.StoreName,
            VAT: request.VAT,
            CRN: request.CRN,
            ShopImageUrl: shopImageUrl,
            Street: request.NationalAddress?.Street,
            BuildingNumber: request.NationalAddress?.BuildingNumber,
            PostalCode: request.NationalAddress?.PostalCode,
            SubNumber: request.NationalAddress?.SubNumber
        );

        var registrationJson = JsonSerializer.Serialize(registrationData);

        // 6. Send OTP
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson, ct);
        if (otpResult.IsFailure)
        {
            // Cleanup uploaded image if OTP fails
            if (shopImageUrl != null)
                await _fileStorageService.DeleteAsync(shopImageUrl);
            return Result.Failure<SendOtpResponse>(otpResult.Error);
        }

        _logger.LogInformation(
            "OTP sent for ShopOwner registration. Mobile: {Mobile}, CustomerCode: {CustomerCode}",
            MobileNumberHelper.Mask(request.MobileNumber),
            request.CustomerCode);

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

        // 2. Validate CustomerCode exists in ErpCustomers
        var erpCustomer = await _context.ErpCustomers
            .FirstOrDefaultAsync(e => e.CustomerCode == request.CustomerCode, ct);

        if (erpCustomer == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.CustomerCodeNotFound);

        // 3. Check if ShopData already exists for this CustomerCode
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
                || string.IsNullOrEmpty(request.CityId) || request.NationalAddress == null)
                return Result.Failure<SendOtpResponse>(AuthErrors.ShopDataRequired);

            // Validate VAT/CRN uniqueness against ShopData table
            var uniqueValidation = await ValidateUniqueFieldsAsync(request.VAT, request.CRN, ct);
            if (uniqueValidation.IsFailure)
                return Result.Failure<SendOtpResponse>(uniqueValidation.Error);

            // Upload shop image
            using var imageStream = request.ShopImage.OpenReadStream();
            var imageResult = await _fileStorageService.UploadAsync(imageStream, request.ShopImage.FileName, "shops", ct);
            if (imageResult.IsFailure)
                return Result.Failure<SendOtpResponse>(AuthErrors.ImageUploadFailed);

            shopImageUrl = imageResult.Value;
            cityId = request.CityId;
        }
        else
        {
            // Use ShopData's city
            cityId = existingShopData!.CityId;
        }

        // 4. Validate city and get SalesMan
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == cityId && c.IsActive, ct);

        if (city == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 5. Serialize registration data
        var registrationData = new SellerRegistrationData(
            UserType: UserType.Seller,
            Name: request.Name,
            MobileNumber: request.MobileNumber,
            CustomerCode: request.CustomerCode,
            AssignedSalesManId: city.ApprovalSalesManId,
            CityId: cityId,
            ShopDataAlreadyExists: shopDataExists,
            StoreName: request.StoreName,
            VAT: request.VAT,
            CRN: request.CRN,
            ShopImageUrl: shopImageUrl,
            Street: request.NationalAddress?.Street,
            BuildingNumber: request.NationalAddress?.BuildingNumber,
            PostalCode: request.NationalAddress?.PostalCode,
            SubNumber: request.NationalAddress?.SubNumber
        );

        var registrationJson = JsonSerializer.Serialize(registrationData);

        // 6. Send OTP
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson, ct);
        if (otpResult.IsFailure)
        {
            if (shopImageUrl != null)
                await _fileStorageService.DeleteAsync(shopImageUrl);
            return Result.Failure<SendOtpResponse>(otpResult.Error);
        }

        _logger.LogInformation(
            "OTP sent for Seller registration. Mobile: {Mobile}, CustomerCode: {CustomerCode}",
            MobileNumberHelper.Mask(request.MobileNumber),
            request.CustomerCode);

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

        // 2. Validate city (no RegionId filter) and has ApprovalSalesManId
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive, ct);

        if (city == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<SendOtpResponse>(AuthErrors.NoApprovalSalesMan);

        // 3. Serialize registration data
        var registrationData = new TechnicianRegistrationData(
            UserType: UserType.Technician,
            Name: request.Name,
            MobileNumber: request.MobileNumber,
            CityId: request.CityId,
            PostalCode: request.PostalCode,
            AssignedSalesManId: city.ApprovalSalesManId
        );

        var registrationJson = JsonSerializer.Serialize(registrationData);

        // 4. Send OTP
        var otpResult = await _otpService.SendAsync(request.MobileNumber, registrationJson, ct);
        if (otpResult.IsFailure)
            return Result.Failure<SendOtpResponse>(otpResult.Error);

        _logger.LogInformation(
            "OTP sent for Technician registration. Mobile: {Mobile}, City: {CityId}",
            MobileNumberHelper.Mask(request.MobileNumber),
            request.CityId);

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

        if (user.RegistrationStatus == RegistrationStatus.Rejected)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserRejected);

        if (user.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<SendOtpResponse>(AuthErrors.UserNotApproved);

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

    #region Private — User Creation Methods

    private async Task<Result<RegisterResponse>> CreateShopOwnerAsync(string registrationJson, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<ShopOwnerRegistrationData>(registrationJson);
        if (data == null)
            return Result.Failure<RegisterResponse>(AuthErrors.RegistrationDataNotFound);

        // Re-validate unique constraints (race condition protection)
        if (await _userRepository.MobileExistsAsync(data.MobileNumber, ct))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileAlreadyRegistered);

        // Re-validate CustomerCode
        var erpExists = await _context.ErpCustomers.AnyAsync(e => e.CustomerCode == data.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<RegisterResponse>(AuthErrors.CustomerCodeNotFound);

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
                    Street = data.Street ?? string.Empty,
                    BuildingNumber = data.BuildingNumber ?? 0,
                    PostalCode = data.PostalCode ?? string.Empty,
                    SubNumber = data.SubNumber ?? 0
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

            // Create ShopData if it doesn't already exist (race condition check)
            if (!data.ShopDataAlreadyExists)
            {
                var shopDataStillMissing = !await _context.ShopData
                    .AnyAsync(sd => sd.CustomerCode == data.CustomerCode, ct);

                if (shopDataStillMissing)
                {
                    var shopData = new ShopData
                    {
                        CustomerCode = data.CustomerCode,
                        StoreName = data.StoreName!,
                        VAT = data.VAT!,
                        CRN = data.CRN!,
                        ShopImageUrl = data.ShopImageUrl!,
                        CityId = data.CityId,
                        Street = data.Street ?? string.Empty,
                        BuildingNumber = data.BuildingNumber ?? 0,
                        PostalCode = data.PostalCode ?? string.Empty,
                        SubNumber = data.SubNumber ?? 0,
                        EnteredByUserId = user.Id,
                        CreatedBy = user.Id
                    };
                    await _context.ShopData.AddAsync(shopData, ct);
                }
            }

            // If ShopData exists, update user's NationalAddress from ShopData
            if (data.ShopDataAlreadyExists)
            {
                var existingShopData = await _context.ShopData
                    .FirstOrDefaultAsync(sd => sd.CustomerCode == data.CustomerCode, ct);

                if (existingShopData != null)
                {
                    user.NationalAddress = new NationalAddress
                    {
                        CityId = existingShopData.CityId,
                        Street = existingShopData.Street,
                        BuildingNumber = existingShopData.BuildingNumber,
                        PostalCode = existingShopData.PostalCode,
                        SubNumber = existingShopData.SubNumber
                    };
                    await _userRepository.UpdateAsync(user);
                }
            }

            var profile = new ShopOwnerProfile
            {
                UserId = user.Id,
                CustomerCode = data.CustomerCode,
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

        // Re-validate CustomerCode
        var erpExists = await _context.ErpCustomers.AnyAsync(e => e.CustomerCode == data.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<RegisterResponse>(AuthErrors.CustomerCodeNotFound);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            // Determine address from ShopData or from registration data
            string cityId = data.CityId;
            string street = data.Street ?? string.Empty;
            int buildingNumber = data.BuildingNumber ?? 0;
            string postalCode = data.PostalCode ?? string.Empty;
            int subNumber = data.SubNumber ?? 0;

            if (data.ShopDataAlreadyExists)
            {
                var existingShopData = await _context.ShopData
                    .FirstOrDefaultAsync(sd => sd.CustomerCode == data.CustomerCode, ct);

                if (existingShopData != null)
                {
                    cityId = existingShopData.CityId;
                    street = existingShopData.Street;
                    buildingNumber = existingShopData.BuildingNumber;
                    postalCode = existingShopData.PostalCode;
                    subNumber = existingShopData.SubNumber;
                }
            }

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
                    CityId = cityId,
                    Street = street,
                    BuildingNumber = buildingNumber,
                    PostalCode = postalCode,
                    SubNumber = subNumber
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

            // Create ShopData if needed
            if (!data.ShopDataAlreadyExists)
            {
                var shopDataStillMissing = !await _context.ShopData
                    .AnyAsync(sd => sd.CustomerCode == data.CustomerCode, ct);

                if (shopDataStillMissing)
                {
                    var shopData = new ShopData
                    {
                        CustomerCode = data.CustomerCode,
                        StoreName = data.StoreName!,
                        VAT = data.VAT!,
                        CRN = data.CRN!,
                        ShopImageUrl = data.ShopImageUrl!,
                        CityId = data.CityId,
                        Street = data.Street ?? string.Empty,
                        BuildingNumber = data.BuildingNumber ?? 0,
                        PostalCode = data.PostalCode ?? string.Empty,
                        SubNumber = data.SubNumber ?? 0,
                        EnteredByUserId = user.Id,
                        CreatedBy = user.Id
                    };
                    await _context.ShopData.AddAsync(shopData, ct);
                }
            }

            var profile = new SellerProfile
            {
                UserId = user.Id,
                CustomerCode = data.CustomerCode,
                CreatedBy = user.Id
            };

            await _context.SellerProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Seller registered successfully. UserId: {UserId}, Mobile: {Mobile}, CustomerCode: {CustomerCode}",
                user.Id,
                MobileNumberHelper.Mask(data.MobileNumber),
                data.CustomerCode);

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

        // Re-validate city still valid and has a SalesMan
        var city = await _context.Cities
            .FirstOrDefaultAsync(c =>
                c.Id == data.CityId &&
                c.IsActive &&
                !string.IsNullOrEmpty(c.ApprovalSalesManId), ct);

        if (city == null)
            return Result.Failure<RegisterResponse>(AuthErrors.CityNotFound);

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

    private async Task<Result> ValidateUniqueFieldsAsync(string vat, string crn, CancellationToken ct = default)
    {
        // Check VAT/CRN uniqueness against ShopData table
        if (await _context.ShopData.AnyAsync(sd => sd.VAT == vat, ct))
            return Result.Failure(AuthErrors.VatAlreadyExists);

        if (await _context.ShopData.AnyAsync(sd => sd.CRN == crn, ct))
            return Result.Failure(AuthErrors.CrnAlreadyExists);

        return Result.Success();
    }

    #endregion
}
