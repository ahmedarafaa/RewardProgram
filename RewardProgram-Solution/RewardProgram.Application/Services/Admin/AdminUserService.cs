using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Application.Interfaces.Files;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Admin;

public class AdminUserService : IAdminUserService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IFileStorageService fileStorageService,
        ILogger<AdminUserService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<Result<AdminAddUserResponse>> AddSalesManAsync(
        AdminAddSalesManRequest request, string adminUserId, CancellationToken ct = default)
    {
        // 1. Check mobile uniqueness
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        // 2. Load and verify all cities
        var cities = await _context.Cities
            .Where(c => request.CityIds.Contains(c.Id) && c.IsActive && !c.IsDeleted)
            .ToListAsync(ct);

        if (cities.Count != request.CityIds.Count)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.SomeCitiesNotFound);

        // 3. Transaction: create user + role + assign cities
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = request.MobileNumber,
                PhoneNumber = request.MobileNumber,
                Name = request.Name,
                MobileNumber = request.MobileNumber,
                UserType = UserType.SalesMan,
                RegistrationStatus = RegistrationStatus.Approved
            };

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to create SalesMan: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.SalesMan);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to add SalesMan role to {UserId}: {Errors}",
                    user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            // Assign SalesMan to each city
            foreach (var city in cities)
            {
                city.ApprovalSalesManId = user.Id;
                city.UpdatedBy = adminUserId;
                city.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} created SalesMan {UserId} for {Count} cities",
                adminUserId, user.Id, cities.Count);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.SalesMan, "تم إنشاء مندوب المبيعات بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to create SalesMan for mobile {Mobile}",
                MobileNumberHelper.Mask(request.MobileNumber));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> AddZoneManagerAsync(
        AdminAddZoneManagerRequest request, string adminUserId, CancellationToken ct = default)
    {
        // 1. Check mobile uniqueness
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        // 2. Load region — verify exists, active, no existing ZoneManager
        var region = await _context.Regions
            .FirstOrDefaultAsync(r => r.Id == request.RegionId && r.IsActive && !r.IsDeleted, ct);

        if (region == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionNotFound);

        if (!string.IsNullOrEmpty(region.ZoneManagerId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionAlreadyHasZoneManager);

        // 3. Transaction: create user + role + assign to region
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = request.MobileNumber,
                PhoneNumber = request.MobileNumber,
                Name = request.Name,
                MobileNumber = request.MobileNumber,
                UserType = UserType.ZoneManager,
                RegistrationStatus = RegistrationStatus.Approved
            };

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to create ZoneManager: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.ZoneManager);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to add ZoneManager role to {UserId}: {Errors}",
                    user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            region.ZoneManagerId = user.Id;
            region.UpdatedBy = adminUserId;
            region.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} created ZoneManager {UserId} for region {RegionId}",
                adminUserId, user.Id, region.Id);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.ZoneManager, "تم إنشاء مدير المنطقة بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to create ZoneManager for mobile {Mobile}",
                MobileNumberHelper.Mask(request.MobileNumber));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> AddShopOwnerAsync(
        AdminAddShopOwnerRequest request, string adminUserId, CancellationToken ct = default)
    {
        // 1. Check mobile uniqueness
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        // 2. Validate CustomerCode exists
        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        // 3. Validate city
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.NoApprovalSalesMan);

        // 4. Check ShopData existence
        var shopDataExists = await _context.ShopData
            .AnyAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

        string? shopImageUrl = null;

        if (!shopDataExists)
        {
            // Shop data required — validate fields
            if (string.IsNullOrEmpty(request.StoreName) || string.IsNullOrEmpty(request.VAT)
                || string.IsNullOrEmpty(request.CRN) || request.ShopImage == null
                || request.NationalAddress == null)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ShopDataRequired);

            // Validate VAT/CRN uniqueness
            if (await _context.ShopData.AnyAsync(sd => sd.VAT == request.VAT, ct))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.VatAlreadyExists);
            if (await _context.ShopData.AnyAsync(sd => sd.CRN == request.CRN, ct))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CrnAlreadyExists);

            // Upload image
            using var imageStream = request.ShopImage.OpenReadStream();
            var imageResult = await _fileStorageService.UploadAsync(imageStream, request.ShopImage.FileName, "shops", ct);
            if (imageResult.IsFailure)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ImageUploadFailed);

            shopImageUrl = imageResult.Value;
        }

        // 5. Transaction: create user + role + ShopData + profile
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = request.MobileNumber,
                PhoneNumber = request.MobileNumber,
                Name = request.OwnerName,
                MobileNumber = request.MobileNumber,
                UserType = UserType.ShopOwner,
                RegistrationStatus = RegistrationStatus.Approved,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = !shopDataExists && request.NationalAddress != null
                    ? new NationalAddress
                    {
                        CityId = request.CityId,
                        Street = request.NationalAddress.Street,
                        BuildingNumber = request.NationalAddress.BuildingNumber,
                        PostalCode = request.NationalAddress.PostalCode,
                        SubNumber = request.NationalAddress.SubNumber
                    }
                    : new NationalAddress { CityId = request.CityId }
            };

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to create ShopOwner: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.ShopOwner);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to add ShopOwner role to {UserId}: {Errors}",
                    user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            // Create ShopData if needed
            if (!shopDataExists)
            {
                var shopData = new ShopData
                {
                    CustomerCode = request.CustomerCode,
                    StoreName = request.StoreName!,
                    VAT = request.VAT!,
                    CRN = request.CRN!,
                    ShopImageUrl = shopImageUrl!,
                    CityId = request.CityId,
                    Street = request.NationalAddress!.Street,
                    BuildingNumber = request.NationalAddress.BuildingNumber,
                    PostalCode = request.NationalAddress.PostalCode,
                    SubNumber = request.NationalAddress.SubNumber,
                    EnteredByUserId = user.Id,
                    CreatedBy = adminUserId
                };
                await _context.ShopData.AddAsync(shopData, ct);
            }

            var profile = new ShopOwnerProfile
            {
                UserId = user.Id,
                CustomerCode = request.CustomerCode,
                CreatedBy = adminUserId
            };
            await _context.ShopOwnerProfiles.AddAsync(profile, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} created ShopOwner {UserId} with CustomerCode {Code}",
                adminUserId, user.Id, request.CustomerCode);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.ShopOwner, "تم إنشاء صاحب المحل بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to create ShopOwner for mobile {Mobile}",
                MobileNumberHelper.Mask(request.MobileNumber));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> AddSellerAsync(
        AdminAddSellerRequest request, string adminUserId, CancellationToken ct = default)
    {
        // 1. Check mobile uniqueness
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        // 2. Validate CustomerCode exists
        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        // 3. Validate city
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.NoApprovalSalesMan);

        // 4. Check ShopData existence
        var shopDataExists = await _context.ShopData
            .AnyAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

        string? shopImageUrl = null;

        if (!shopDataExists)
        {
            if (string.IsNullOrEmpty(request.StoreName) || string.IsNullOrEmpty(request.VAT)
                || string.IsNullOrEmpty(request.CRN) || request.ShopImage == null
                || request.NationalAddress == null)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ShopDataRequired);

            if (await _context.ShopData.AnyAsync(sd => sd.VAT == request.VAT, ct))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.VatAlreadyExists);
            if (await _context.ShopData.AnyAsync(sd => sd.CRN == request.CRN, ct))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CrnAlreadyExists);

            using var imageStream = request.ShopImage.OpenReadStream();
            var imageResult = await _fileStorageService.UploadAsync(imageStream, request.ShopImage.FileName, "shops", ct);
            if (imageResult.IsFailure)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ImageUploadFailed);

            shopImageUrl = imageResult.Value;
        }

        // 5. Transaction: create user + role + ShopData + profile
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = request.MobileNumber,
                PhoneNumber = request.MobileNumber,
                Name = request.Name,
                MobileNumber = request.MobileNumber,
                UserType = UserType.Seller,
                RegistrationStatus = RegistrationStatus.Approved,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = !shopDataExists && request.NationalAddress != null
                    ? new NationalAddress
                    {
                        CityId = request.CityId,
                        Street = request.NationalAddress.Street,
                        BuildingNumber = request.NationalAddress.BuildingNumber,
                        PostalCode = request.NationalAddress.PostalCode,
                        SubNumber = request.NationalAddress.SubNumber
                    }
                    : new NationalAddress { CityId = request.CityId }
            };

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to create Seller: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.Seller);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to add Seller role to {UserId}: {Errors}",
                    user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            if (!shopDataExists)
            {
                var shopData = new ShopData
                {
                    CustomerCode = request.CustomerCode,
                    StoreName = request.StoreName!,
                    VAT = request.VAT!,
                    CRN = request.CRN!,
                    ShopImageUrl = shopImageUrl!,
                    CityId = request.CityId,
                    Street = request.NationalAddress!.Street,
                    BuildingNumber = request.NationalAddress.BuildingNumber,
                    PostalCode = request.NationalAddress.PostalCode,
                    SubNumber = request.NationalAddress.SubNumber,
                    EnteredByUserId = user.Id,
                    CreatedBy = adminUserId
                };
                await _context.ShopData.AddAsync(shopData, ct);
            }

            var profile = new SellerProfile
            {
                UserId = user.Id,
                CustomerCode = request.CustomerCode,
                CreatedBy = adminUserId
            };
            await _context.SellerProfiles.AddAsync(profile, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} created Seller {UserId} with CustomerCode {Code}",
                adminUserId, user.Id, request.CustomerCode);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.Seller, "تم إنشاء البائع بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to create Seller for mobile {Mobile}",
                MobileNumberHelper.Mask(request.MobileNumber));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> AddTechnicianAsync(
        AdminAddTechnicianRequest request, string adminUserId, CancellationToken ct = default)
    {
        // 1. Check mobile uniqueness
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        // 2. Validate city
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.NoApprovalSalesMan);

        // 3. Transaction: create user + role + profile
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = request.MobileNumber,
                PhoneNumber = request.MobileNumber,
                Name = request.Name,
                MobileNumber = request.MobileNumber,
                UserType = UserType.Technician,
                RegistrationStatus = RegistrationStatus.Approved,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = request.CityId,
                    PostalCode = request.PostalCode
                }
            };

            var createResult = await _userRepository.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to create Technician: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.Technician);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to add Technician role to {UserId}: {Errors}",
                    user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
            }

            var profile = new TechnicianProfile
            {
                UserId = user.Id,
                CreatedBy = adminUserId
            };
            await _context.TechnicianProfiles.AddAsync(profile, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} created Technician {UserId}",
                adminUserId, user.Id);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.Technician, "تم إنشاء الفني بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to create Technician for mobile {Mobile}",
                MobileNumberHelper.Mask(request.MobileNumber));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CreateUserFailed);
        }
    }
}
