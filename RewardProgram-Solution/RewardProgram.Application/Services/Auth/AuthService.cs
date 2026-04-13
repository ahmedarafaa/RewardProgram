using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NanoidDotNet;
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
    private readonly VerificationTokenOptions _verificationTokenOptions;

    public AuthService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IOtpService otpService,
        IFileStorageService fileStorageService,
        ITokenService tokenService,
        ILogger<AuthService> logger,
        IOptions<VerificationTokenOptions> verificationTokenOptions)
    {
        _context = context;
        _userRepository = userRepository;
        _otpService = otpService;
        _fileStorageService = fileStorageService;
        _tokenService = tokenService;
        _logger = logger;
        _verificationTokenOptions = verificationTokenOptions.Value;
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

    public async Task<Result<VerifyRegistrationOtpResponse>> VerifyRegistrationOtpAsync(
        VerifyRegistrationOtpRequest request, CancellationToken ct = default)
    {
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        // Check mobile not already registered
        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<VerifyRegistrationOtpResponse>(AuthErrors.MobileAlreadyRegistered);

        // Verify OTP with Twilio (consumes the OTP)
        var verifyResult = await _otpService.VerifyAsync(request.PinId, request.Otp, ct);
        if (verifyResult.IsFailure)
            return Result.Failure<VerifyRegistrationOtpResponse>(verifyResult.Error);

        if (verifyResult.Value.MobileNumber != mobile)
            return Result.Failure<VerifyRegistrationOtpResponse>(AuthErrors.MobileMismatch);

        // Generate signed verification token
        var token = RegistrationVerificationToken.Generate(
            mobile, _verificationTokenOptions.HmacKey, _verificationTokenOptions.ExpiryMinutes);

        _logger.LogInformation(
            "Registration OTP verified. Mobile: {Mobile}",
            MobileNumberHelper.Mask(mobile));

        return Result.Success(new VerifyRegistrationOtpResponse(
            VerificationToken: token,
            MaskedMobileNumber: MobileNumberHelper.Mask(mobile)
        ));
    }

    public async Task<Result<RegisterResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request, CancellationToken ct = default)
    {
        // 1. Validate verification token and extract mobile
        var tokenResult = RegistrationVerificationToken.Validate(
            request.VerificationToken, _verificationTokenOptions.HmacKey);
        if (tokenResult.IsFailure)
            return Result.Failure<RegisterResponse>(tokenResult.Error);

        var mobile = tokenResult.Value;

        if (mobile != MobileNumberHelper.Normalize(request.MobileNumber))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileMismatch);

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

        // Validate invitation code (if provided)
        var inviteResult = await ValidateInvitationCodeAsync(request.InvitationCode, mobile, ct);
        if (inviteResult.IsFailure)
            return Result.Failure<RegisterResponse>(inviteResult.Error);

        // 2. Create user in transaction
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
                InvitationCode = await GenerateUniqueInvitationCodeAsync(ct),
                InvitedByUserId = inviteResult.Value,
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
        // 1. Validate verification token and extract mobile
        var tokenResult = RegistrationVerificationToken.Validate(
            request.VerificationToken, _verificationTokenOptions.HmacKey);
        if (tokenResult.IsFailure)
            return Result.Failure<RegisterResponse>(tokenResult.Error);

        var mobile = tokenResult.Value;

        if (mobile != MobileNumberHelper.Normalize(request.MobileNumber))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileMismatch);

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

        // Validate invitation code (if provided)
        var inviteResult = await ValidateInvitationCodeAsync(request.InvitationCode, mobile, ct);
        if (inviteResult.IsFailure)
            return Result.Failure<RegisterResponse>(inviteResult.Error);

        // 2. Create user in transaction
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
                InvitationCode = await GenerateUniqueInvitationCodeAsync(ct),
                InvitedByUserId = inviteResult.Value,
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
        // 1. Validate verification token and extract mobile
        var tokenResult = RegistrationVerificationToken.Validate(
            request.VerificationToken, _verificationTokenOptions.HmacKey);
        if (tokenResult.IsFailure)
            return Result.Failure<RegisterResponse>(tokenResult.Error);

        var mobile = tokenResult.Value;

        if (mobile != MobileNumberHelper.Normalize(request.MobileNumber))
            return Result.Failure<RegisterResponse>(AuthErrors.MobileMismatch);

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

        // Validate invitation code (if provided)
        var inviteResult = await ValidateInvitationCodeAsync(request.InvitationCode, mobile, ct);
        if (inviteResult.IsFailure)
            return Result.Failure<RegisterResponse>(inviteResult.Error);

        // 2. Create user in transaction
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
                InvitationCode = await GenerateUniqueInvitationCodeAsync(ct),
                InvitedByUserId = inviteResult.Value,
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

        if (user.RegistrationStatus == RegistrationStatus.Rejected)
            return Result.Failure<AuthResponse>(AuthErrors.UserRejected);

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
        => await RefreshTokenInternalAsync(refreshToken, isAdmin: false, ct);

    public async Task<Result<AuthResponse>> RefreshAdminTokenAsync(string refreshToken, CancellationToken ct = default)
        => await RefreshTokenInternalAsync(refreshToken, isAdmin: true, ct);

    private async Task<Result<AuthResponse>> RefreshTokenInternalAsync(string refreshToken, bool isAdmin, CancellationToken ct)
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

        // Scope enforcement: admin refresh endpoint only accepts SystemAdmin users;
        // public refresh endpoint rejects SystemAdmin users (prevents cross-scope refresh).
        var roles = await _userRepository.GetRolesAsync(user);
        var isSystemAdmin = roles.Contains(UserRoles.SystemAdmin);

        if (isAdmin && !isSystemAdmin)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        if (!isAdmin && isSystemAdmin)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        // Revoke old token
        token.RevokedOn = DateTime.UtcNow;

        // Clean up only tokens that are both revoked AND expired (safe for multi-device)
        user.RefreshTokens.RemoveAll(t => t.Token != refreshToken && t.RevokedOn != null && t.IsExpired);

        // Generate new auth response (adds new token to user.RefreshTokens)
        // Single UpdateAsync call persists both revocation and new token atomically
        var authResponse = isAdmin
            ? await _tokenService.GenerateAdminAuthResponseAsync(user)
            : await _tokenService.GenerateAuthResponseAsync(user);

        _logger.LogInformation("Token refreshed for UserId: {UserId} (admin={IsAdmin})", user.Id, isAdmin);

        return Result.Success(authResponse);
    }

    public async Task<Result> RevokeTokenAsync(string refreshToken, string currentUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByRefreshTokenAsync(refreshToken, ct);

        if (user == null)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        // Ownership check: only allow revoking your own tokens
        if (user.Id != currentUserId)
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

    #region Invitation Helpers

    private const string InviteAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int InviteCodeLength = 8;

    private async Task<Result<string?>> ValidateInvitationCodeAsync(string? invitationCode, string mobile, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(invitationCode))
            return Result.Success<string?>(null);

        var inviter = await _userRepository.Query()
            .FirstOrDefaultAsync(u => u.InvitationCode == invitationCode, ct);

        if (inviter is null)
            return Result.Failure<string?>(InvitationErrors.InvalidInvitationCode);

        if (inviter.MobileNumber == mobile)
            return Result.Failure<string?>(InvitationErrors.SelfInvitation);

        if (inviter.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<string?>(InvitationErrors.InviterNotApproved);

        return Result.Success<string?>(inviter.Id);
    }

    private async Task<string> GenerateUniqueInvitationCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = Nanoid.Generate(InviteAlphabet, InviteCodeLength);
            var exists = await _userRepository.Query()
                .AnyAsync(u => u.InvitationCode == code, ct);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Failed to generate unique invitation code after 10 attempts");
    }

    #endregion

}
