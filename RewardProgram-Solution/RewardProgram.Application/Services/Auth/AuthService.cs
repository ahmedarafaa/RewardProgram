using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.DTOs.Auth;
using RewardProgram.Application.DTOs.Auth.Additional;
using RewardProgram.Application.DTOs.Auth.UsersDTO;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;
using RewardProgram.Infrastructure.Authentication;
using RewardProgram.Infrastructure.Persistance;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RewardProgram.Application.Services.Auth;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpService _otpService;
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<AuthService> _logger;

    private const int REFRESH_TOKEN_EXPIRY_DAYS = 7;

    public AuthService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOtpService otpService,
        IJwtProvider jwtProvider,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userManager = userManager;
        _otpService = otpService;
        _jwtProvider = jwtProvider;
        _logger = logger;
    }

    #region Registration

    public async Task<Result<RegistrationResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request)
    {
        // Check if mobile already exists
        var existingUser = await _context.Users
            .AnyAsync(u => u.MobileNumber == request.MobileNumber);

        if (existingUser)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.MobileAlreadyRegistered);
        }

        // Check if SalesMan exists for the city
        var salesManMapping = await _context.CitySalesManMappings
            .FirstOrDefaultAsync(c => c.City == request.NationalAddress.City && c.IsActive);

        if (salesManMapping == null)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.NoSalesManForCity);
        }

        // Serialize registration data
        var registrationData = JsonSerializer.Serialize(new
        {
            request.OwnerName,
            request.MobileNumber,
            request.StoreName,
            request.VAT,
            request.CRN,
            request.ShopImageUrl,
            request.NationalAddress.BuildingNumber,
            request.NationalAddress.City,
            request.NationalAddress.Street,
            request.NationalAddress.Neighborhood,
            request.NationalAddress.PostalCode,
            request.NationalAddress.SubNumber,
            SalesManId = salesManMapping.SalesManId,
            UserType = UserType.ShopOwner
        });

        // Generate and send OTP
        var otpResult = await _otpService.GenerateAndSendAsync(
            request.MobileNumber,
            OtpPurpose.Registration,
            registrationData);

        if (otpResult.IsFailure)
        {
            return Result.Failure<RegistrationResponse>(otpResult.Error);
        }

        return Result.Success(new RegistrationResponse(
            Message: "تم إرسال رمز التحقق",
            OtpExpiresInSeconds: 300
        ));
    }

    public async Task<Result<RegistrationResponse>> RegisterSellerAsync(RegisterSellerRequest request)
    {
        // Check if mobile already exists
        var existingUser = await _context.Users
            .AnyAsync(u => u.MobileNumber == request.MobileNumber);

        if (existingUser)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.MobileAlreadyRegistered);
        }

        // Validate ShopCode
        var shopOwnerProfile = await _context.ShopOwnerProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ShopCode == request.ShopCode);

        if (shopOwnerProfile == null)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.InvalidShopCode);
        }

        if (shopOwnerProfile.User.RegistrationStatus != RegistrationStatus.Approved)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.ShopOwnerNotApproved);
        }

        // Get SalesMan from ShopOwner
        var salesManId = shopOwnerProfile.User.AssignedSalesManId;

        // Serialize registration data
        var registrationData = JsonSerializer.Serialize(new
        {
            request.Name,
            request.MobileNumber,
            request.ShopCode,
            ShopOwnerId = shopOwnerProfile.UserId,
            request.NationalAddress.BuildingNumber,
            request.NationalAddress.City,
            request.NationalAddress.Street,
            request.NationalAddress.Neighborhood,
            request.NationalAddress.PostalCode,
            request.NationalAddress.SubNumber,
            SalesManId = salesManId,
            UserType = UserType.Seller
        });

        // Generate and send OTP
        var otpResult = await _otpService.GenerateAndSendAsync(
            request.MobileNumber,
            OtpPurpose.Registration,
            registrationData);

        if (otpResult.IsFailure)
        {
            return Result.Failure<RegistrationResponse>(otpResult.Error);
        }

        return Result.Success(new RegistrationResponse(
            Message: "تم إرسال رمز التحقق",
            OtpExpiresInSeconds: 300
        ));
    }

    public async Task<Result<RegistrationResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request)
    {
        // Check if mobile already exists
        var existingUser = await _context.Users
            .AnyAsync(u => u.MobileNumber == request.MobileNumber);

        if (existingUser)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.MobileAlreadyRegistered);
        }

        // Check if SalesMan exists for the city
        var salesManMapping = await _context.CitySalesManMappings
            .FirstOrDefaultAsync(c => c.City == request.NationalAddress.City && c.IsActive);

        if (salesManMapping == null)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.NoSalesManForCity);
        }

        // Serialize registration data
        var registrationData = JsonSerializer.Serialize(new
        {
            request.Name,
            request.MobileNumber,
            request.NationalAddress.BuildingNumber,
            request.NationalAddress.City,
            request.NationalAddress.Street,
            request.NationalAddress.Neighborhood,
            request.NationalAddress.PostalCode,
            request.NationalAddress.SubNumber,
            SalesManId = salesManMapping.SalesManId,
            UserType = UserType.Technician
        });

        // Generate and send OTP
        var otpResult = await _otpService.GenerateAndSendAsync(
            request.MobileNumber,
            OtpPurpose.Registration,
            registrationData);

        if (otpResult.IsFailure)
        {
            return Result.Failure<RegistrationResponse>(otpResult.Error);
        }

        return Result.Success(new RegistrationResponse(
            Message: "تم إرسال رمز التحقق",
            OtpExpiresInSeconds: 300
        ));
    }

    public async Task<Result> VerifyRegistrationAsync(VerifyOtpRequest request)
    {
        // Verify OTP with mobile number to prevent OTP hijacking
        var otpResult = await _otpService.VerifyAsync(request.MobileNumber, request.Otp, OtpPurpose.Registration);

        if (otpResult.IsFailure)
        {
            return Result.Failure(otpResult.Error);
        }

        var otpCode = otpResult.Value;

        if (string.IsNullOrEmpty(otpCode.RegistrationData))
        {
            return Result.Failure(AuthErrors.RegistrationDataNotFound);
        }

        // Deserialize registration data
        using var jsonDoc = JsonDocument.Parse(otpCode.RegistrationData);
        var root = jsonDoc.RootElement;

        var userType = (UserType)root.GetProperty("UserType").GetInt32();

        // Create user based on type
        var user = new ApplicationUser
        {
            UserName = otpCode.MobileNumber,
            MobileNumber = otpCode.MobileNumber,
            PhoneNumber = otpCode.MobileNumber,
            UserType = userType,
            RegistrationStatus = RegistrationStatus.PendingSalesman,
            AssignedSalesManId = root.GetProperty("SalesManId").GetString(),
            PhoneNumberConfirmed = true,
            NationalAddress = new NationalAddress
            {
                BuildingNumber = root.GetProperty("BuildingNumber").GetInt32(),
                City = root.GetProperty("City").GetString() ?? string.Empty,
                Street = root.GetProperty("Street").GetString() ?? string.Empty,
                Neighborhood = root.GetProperty("Neighborhood").GetString() ?? string.Empty,
                PostalCode = root.GetProperty("PostalCode").GetString() ?? string.Empty,
                SubNumber = root.GetProperty("SubNumber").GetInt32()
            }
        };

        // Set name based on user type
        if (userType == UserType.ShopOwner)
        {
            user.Name = root.GetProperty("OwnerName").GetString() ?? string.Empty;
        }
        else
        {
            user.Name = root.GetProperty("Name").GetString() ?? string.Empty;
        }

        // Use transaction to ensure atomicity of user creation, role assignment, and profile creation
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create user
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create user: {Errors}", errors);
                return Result.Failure(new Error("Auth.CreateFailed", errors, StatusCodes.Status500InternalServerError));
            }

            // Add role
            var roleName = userType.ToString();
            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to assign role: {Errors}", errors);
                return Result.Failure(new Error("Auth.RoleAssignFailed", errors, StatusCodes.Status500InternalServerError));
            }

            // Create profile based on type
            switch (userType)
            {
                case UserType.ShopOwner:
                    var shopOwnerProfile = new ShopOwnerProfile
                    {
                        UserId = user.Id,
                        StoreName = root.GetProperty("StoreName").GetString() ?? string.Empty,
                        VAT = root.GetProperty("VAT").GetString() ?? string.Empty,
                        CRN = root.GetProperty("CRN").GetString() ?? string.Empty,
                        ShopImageUrl = root.GetProperty("ShopImageUrl").GetString() ?? string.Empty,
                        ShopCode = await GenerateUniqueShopCodeAsync()
                    };
                    await _context.ShopOwnerProfiles.AddAsync(shopOwnerProfile);
                    break;

                case UserType.Seller:
                    var sellerProfile = new SellerProfile
                    {
                        UserId = user.Id,
                        ShopOwnerId = root.GetProperty("ShopOwnerId").GetString() ?? string.Empty
                    };
                    await _context.SellerProfiles.AddAsync(sellerProfile);
                    break;

                case UserType.Technician:
                    var technicianProfile = new TechnicianProfile
                    {
                        UserId = user.Id
                    };
                    await _context.TechnicianProfiles.AddAsync(technicianProfile);
                    break;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to complete registration for {MobileNumber}", request.MobileNumber);
            return Result.Failure(new Error("Auth.RegistrationFailed", "فشل في إتمام التسجيل", StatusCodes.Status500InternalServerError));
        }
    }

    private async Task<string> GenerateUniqueShopCodeAsync()
    {
        string code;
        do
        {
            code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        } while (await _context.ShopOwnerProfiles.AnyAsync(s => s.ShopCode == code));

        return code;
    }

    #endregion

    #region Login

    public async Task<Result<RegistrationResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.MobileNumber == request.MobileNumber);

        if (user == null)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.UserNotFound);
        }

        if (user.IsDisabled)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.UserDisabled);
        }

        if (user.RegistrationStatus == RegistrationStatus.Rejected)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.UserRejected);
        }

        if (user.RegistrationStatus != RegistrationStatus.Approved)
        {
            return Result.Failure<RegistrationResponse>(AuthErrors.UserNotApproved);
        }

        // Generate and send OTP
        var otpResult = await _otpService.GenerateAndSendAsync(
            request.MobileNumber,
            OtpPurpose.Login);

        if (otpResult.IsFailure)
        {
            return Result.Failure<RegistrationResponse>(otpResult.Error);
        }

        return Result.Success(new RegistrationResponse(
            Message: "تم إرسال رمز التحقق",
            OtpExpiresInSeconds: 300
        ));
    }

    public async Task<Result<AuthResponse>> VerifyLoginAsync(VerifyLoginRequest request)
    {
        // Verify OTP
        var otpResult = await _otpService.VerifyAsync(
            request.MobileNumber,
            request.Otp,
            OtpPurpose.Login);

        if (otpResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(otpResult.Error);
        }

        // Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.MobileNumber == request.MobileNumber);

        if (user == null)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserNotFound);
        }

        // Re-validate user status (could have changed between LoginAsync and VerifyLoginAsync)
        if (user.IsDisabled)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserDisabled);
        }

        if (user.RegistrationStatus == RegistrationStatus.Rejected)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserRejected);
        }

        if (user.RegistrationStatus != RegistrationStatus.Approved)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserNotApproved);
        }

        // Generate tokens
        var (accessToken, expiresIn) = _jwtProvider.GenerateToken(user);
        var refreshToken = GenerateRefreshToken();

        // Save refresh token
        user.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return Result.Success(new AuthResponse(
            Token: accessToken,
            RefreshToken: refreshToken.Token,
            ExpiresIn: expiresIn,
            RefreshTokenExpiration: refreshToken.ExpiresOn,
            User: new UserResponse(
                Id: user.Id,
                Name: user.Name,
                MobileNumber: user.MobileNumber,
                UserType: user.UserType
            )
        ));
    }

    #endregion

    #region Token Management

    public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(RefreshTokenResponse request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == request.RefreshToken));

        if (user == null)
        {
            return Result.Failure<RefreshTokenResponse>(AuthErrors.InvalidRefreshToken);
        }

        var refreshToken = user.RefreshTokens.First(t => t.Token == request.RefreshToken);

        if (!refreshToken.IsActive)
        {
            return Result.Failure<RefreshTokenResponse>(AuthErrors.InvalidRefreshToken);
        }

        // Revoke old refresh token
        refreshToken.RevokedOn = DateTime.UtcNow;

        // Generate new tokens
        var (accessToken, _) = _jwtProvider.GenerateToken(user);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        return Result.Success(new RefreshTokenResponse(
            Token: accessToken,
            RefreshToken: newRefreshToken.Token
        ));
    }

    public async Task<Result> RevokeTokenAsync(RefreshTokenResponse request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == request.RefreshToken));

        if (user == null)
        {
            return Result.Failure(AuthErrors.InvalidRefreshToken);
        }

        var refreshToken = user.RefreshTokens.First(t => t.Token == request.RefreshToken);

        if (!refreshToken.IsActive)
        {
            return Result.Failure(AuthErrors.InvalidRefreshToken);
        }

        refreshToken.RevokedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    #endregion

    #region Helpers

    private static RefreshToken GenerateRefreshToken()
    {
        return new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresOn = DateTime.UtcNow.AddDays(REFRESH_TOKEN_EXPIRY_DAYS),
            CreatedOn = DateTime.UtcNow
        };
    }
    public async Task<Result<UserResponse>> GetUserByIdAsync(string userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Result.Failure<UserResponse>(AuthErrors.UserNotFound);
        }

        return Result.Success(new UserResponse(
            Id: user.Id,
            Name: user.Name,
            MobileNumber: user.MobileNumber,
            UserType: user.UserType
        ));
    }

    #endregion
}