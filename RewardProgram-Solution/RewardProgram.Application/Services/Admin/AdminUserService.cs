using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
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

    #region Add User

    public async Task<Result<AdminAddUserResponse>> AddSalesManAsync(
        AdminAddSalesManRequest request, string adminUserId, CancellationToken ct = default)
    {
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var cities = await _context.Cities
            .Where(c => request.CityIds.Contains(c.Id) && c.IsActive && !c.IsDeleted)
            .ToListAsync(ct);

        if (cities.Count != request.CityIds.Count)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.SomeCitiesNotFound);

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
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var region = await _context.Regions
            .FirstOrDefaultAsync(r => r.Id == request.RegionId && r.IsActive && !r.IsDeleted, ct);

        if (region == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionNotFound);

        if (!string.IsNullOrEmpty(region.ZoneManagerId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionAlreadyHasZoneManager);

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
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.NoApprovalSalesMan);

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
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.NoApprovalSalesMan);

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
        if (await _userRepository.MobileExistsAsync(request.MobileNumber, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        if (string.IsNullOrEmpty(city.ApprovalSalesManId))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.NoApprovalSalesMan);

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

    #endregion

    #region List Users

    public async Task<Result<PaginatedResult<AdminUserListItemResponse>>> ListUsersAsync(
        AdminUserListQuery query, CancellationToken ct = default)
    {
        var usersQuery = _userRepository.Query()
            .Where(u => u.UserType != UserType.SystemAdmin);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            usersQuery = usersQuery.Where(u =>
                u.Name.Contains(search) || u.MobileNumber.Contains(search));
        }

        if (query.UserType.HasValue)
            usersQuery = usersQuery.Where(u => u.UserType == query.UserType.Value);

        if (query.RegistrationStatus.HasValue)
            usersQuery = usersQuery.Where(u => u.RegistrationStatus == query.RegistrationStatus.Value);

        if (query.IsDisabled.HasValue)
            usersQuery = usersQuery.Where(u => u.IsDisabled == query.IsDisabled.Value);

        if (!string.IsNullOrWhiteSpace(query.RegionId))
        {
            var cityIdsInRegion = _context.Cities
                .Where(c => c.RegionId == query.RegionId)
                .Select(c => c.Id);

            usersQuery = usersQuery.Where(u =>
                u.NationalAddress != null && cityIdsInRegion.Contains(u.NationalAddress.CityId));
        }

        // Count and paginate
        var totalCount = await usersQuery.CountAsync(ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.MobileNumber,
                u.UserType,
                u.RegistrationStatus,
                u.IsDisabled,
                u.CreatedAt,
                CityId = u.NationalAddress != null ? u.NationalAddress.CityId : null
            })
            .ToListAsync(ct);

        // Bulk-load city + region names
        var cityIds = users
            .Where(u => u.CityId != null)
            .Select(u => u.CityId!)
            .Distinct()
            .ToList();

        var cityMap = cityIds.Count > 0
            ? await _context.Cities
                .Where(c => cityIds.Contains(c.Id))
                .Select(c => new { c.Id, c.NameAr, c.RegionId })
                .ToDictionaryAsync(c => c.Id, ct)
            : [];

        var regionIds = cityMap.Values.Select(c => c.RegionId).Distinct().ToList();
        var regionMap = regionIds.Count > 0
            ? await _context.Regions
                .Where(r => regionIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.NameAr, ct)
            : [];

        // Bulk-load CustomerCode + StoreName for ShopOwner/Seller
        var shopOwnerIds = users.Where(u => u.UserType == UserType.ShopOwner).Select(u => u.Id).ToList();
        var sellerIds = users.Where(u => u.UserType == UserType.Seller).Select(u => u.Id).ToList();

        var shopOwnerProfileMap = shopOwnerIds.Count > 0
            ? await _context.ShopOwnerProfiles
                .Where(p => shopOwnerIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.CustomerCode, ct)
            : [];

        var sellerProfileMap = sellerIds.Count > 0
            ? await _context.SellerProfiles
                .Where(p => sellerIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.CustomerCode, ct)
            : [];

        var allCustomerCodes = shopOwnerProfileMap.Values
            .Concat(sellerProfileMap.Values)
            .Distinct()
            .ToList();

        var shopDataMap = allCustomerCodes.Count > 0
            ? await _context.ShopData
                .Where(sd => allCustomerCodes.Contains(sd.CustomerCode))
                .ToDictionaryAsync(sd => sd.CustomerCode, sd => sd.StoreName, ct)
            : [];

        // Map results
        var items = users.Select(u =>
        {
            string? cityName = null;
            string? regionName = null;

            if (u.CityId != null && cityMap.TryGetValue(u.CityId, out var cityInfo))
            {
                cityName = cityInfo.NameAr;
                if (regionMap.TryGetValue(cityInfo.RegionId, out var rName))
                    regionName = rName;
            }

            string? customerCode = null;
            string? storeName = null;

            if (u.UserType == UserType.ShopOwner && shopOwnerProfileMap.TryGetValue(u.Id, out var soCode))
            {
                customerCode = soCode;
                shopDataMap.TryGetValue(soCode, out storeName);
            }
            else if (u.UserType == UserType.Seller && sellerProfileMap.TryGetValue(u.Id, out var sellerCode))
            {
                customerCode = sellerCode;
                shopDataMap.TryGetValue(sellerCode, out storeName);
            }

            return new AdminUserListItemResponse(
                u.Id, u.Name, u.MobileNumber, u.UserType, u.RegistrationStatus,
                u.IsDisabled, u.CreatedAt, regionName, cityName, customerCode, storeName);
        }).ToList();

        return Result.Success(new PaginatedResult<AdminUserListItemResponse>(items, totalCount, page, pageSize));
    }

    #endregion

    #region Toggle Status

    public async Task<Result<AdminToggleStatusResponse>> ToggleStatusAsync(
        string userId, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);

        if (user == null)
            return Result.Failure<AdminToggleStatusResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType == UserType.SystemAdmin)
            return Result.Failure<AdminToggleStatusResponse>(AdminUserErrors.UserIsSystemAdmin);

        user.IsDisabled = !user.IsDisabled;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Admin: Failed to toggle status for user {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminToggleStatusResponse>(AdminUserErrors.UpdateUserFailed);
        }

        var message = user.IsDisabled ? "تم تعطيل المستخدم بنجاح" : "تم تفعيل المستخدم بنجاح";

        _logger.LogInformation("Admin {AdminId} toggled status for user {UserId} to IsDisabled={IsDisabled}",
            adminUserId, userId, user.IsDisabled);

        return Result.Success(new AdminToggleStatusResponse(user.Id, user.IsDisabled, message));
    }

    #endregion

    #region Edit User

    public async Task<Result<AdminAddUserResponse>> EditSalesManAsync(
        string userId, AdminEditSalesManRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.SalesMan)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        // Check mobile uniqueness (excluding current user)
        if (user.MobileNumber != request.MobileNumber)
        {
            var mobileInUse = await _userRepository.Query()
                .AnyAsync(u => u.MobileNumber == request.MobileNumber && u.Id != userId, ct);
            if (mobileInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyInUse);
        }

        // Load and verify all new cities
        var newCities = await _context.Cities
            .Where(c => request.CityIds.Contains(c.Id) && c.IsActive && !c.IsDeleted)
            .ToListAsync(ct);

        if (newCities.Count != request.CityIds.Count)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.SomeCitiesNotFound);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            // Update user fields
            user.Name = request.Name;
            user.MobileNumber = request.MobileNumber;
            user.UserName = request.MobileNumber;
            user.PhoneNumber = request.MobileNumber;

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to update SalesMan {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
            }

            // Load current cities assigned to this salesman
            var currentCities = await _context.Cities
                .Where(c => c.ApprovalSalesManId == userId)
                .ToListAsync(ct);

            var currentCityIds = currentCities.Select(c => c.Id).ToHashSet();
            var newCityIds = request.CityIds.ToHashSet();

            // Removed cities: unassign salesman
            var removedCities = currentCities.Where(c => !newCityIds.Contains(c.Id)).ToList();
            foreach (var city in removedCities)
            {
                city.ApprovalSalesManId = null;
                city.UpdatedBy = adminUserId;
                city.UpdatedAt = DateTime.UtcNow;
            }

            // Added cities: assign salesman
            var addedCities = newCities.Where(c => !currentCityIds.Contains(c.Id)).ToList();
            foreach (var city in addedCities)
            {
                city.ApprovalSalesManId = userId;
                city.UpdatedBy = adminUserId;
                city.UpdatedAt = DateTime.UtcNow;
            }

            // Reassign users in removed cities: clear AssignedSalesManId
            if (removedCities.Count > 0)
            {
                var removedCityIds = removedCities.Select(c => c.Id).ToList();
                var usersInRemovedCities = await _userRepository.Query()
                    .Where(u => u.AssignedSalesManId == userId
                        && u.NationalAddress != null
                        && removedCityIds.Contains(u.NationalAddress.CityId))
                    .ToListAsync(ct);

                foreach (var u in usersInRemovedCities)
                {
                    u.AssignedSalesManId = null;
                }
            }

            // Reassign users in added cities: set AssignedSalesManId
            if (addedCities.Count > 0)
            {
                var addedCityIds = addedCities.Select(c => c.Id).ToList();
                var usersInAddedCities = await _userRepository.Query()
                    .Where(u => u.UserType != UserType.SalesMan
                        && u.UserType != UserType.ZoneManager
                        && u.UserType != UserType.SystemAdmin
                        && u.NationalAddress != null
                        && addedCityIds.Contains(u.NationalAddress.CityId))
                    .ToListAsync(ct);

                foreach (var u in usersInAddedCities)
                {
                    u.AssignedSalesManId = userId;
                }
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} updated SalesMan {UserId}", adminUserId, userId);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.SalesMan, "تم تعديل مندوب المبيعات بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to update SalesMan {UserId}", userId);
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> EditZoneManagerAsync(
        string userId, AdminEditZoneManagerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.ZoneManager)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        if (user.MobileNumber != request.MobileNumber)
        {
            var mobileInUse = await _userRepository.Query()
                .AnyAsync(u => u.MobileNumber == request.MobileNumber && u.Id != userId, ct);
            if (mobileInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyInUse);
        }

        // Validate new region
        var newRegion = await _context.Regions
            .FirstOrDefaultAsync(r => r.Id == request.RegionId && r.IsActive && !r.IsDeleted, ct);

        if (newRegion == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionNotFound);

        // Check if region already has a different ZoneManager
        if (!string.IsNullOrEmpty(newRegion.ZoneManagerId) && newRegion.ZoneManagerId != userId)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionAlreadyHasZoneManager);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            user.Name = request.Name;
            user.MobileNumber = request.MobileNumber;
            user.UserName = request.MobileNumber;
            user.PhoneNumber = request.MobileNumber;

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to update ZoneManager {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
            }

            // If region changed, clear old and set new
            if (newRegion.ZoneManagerId != userId)
            {
                // Clear old region assignment
                var oldRegion = await _context.Regions
                    .FirstOrDefaultAsync(r => r.ZoneManagerId == userId, ct);

                if (oldRegion != null)
                {
                    oldRegion.ZoneManagerId = null;
                    oldRegion.UpdatedBy = adminUserId;
                    oldRegion.UpdatedAt = DateTime.UtcNow;
                }

                // Set new region
                newRegion.ZoneManagerId = userId;
                newRegion.UpdatedBy = adminUserId;
                newRegion.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} updated ZoneManager {UserId}", adminUserId, userId);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.ZoneManager, "تم تعديل مدير المنطقة بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to update ZoneManager {UserId}", userId);
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> EditShopOwnerAsync(
        string userId, AdminEditShopOwnerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.ShopOwner)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        if (user.MobileNumber != request.MobileNumber)
        {
            var mobileInUse = await _userRepository.Query()
                .AnyAsync(u => u.MobileNumber == request.MobileNumber && u.Id != userId, ct);
            if (mobileInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyInUse);
        }

        // Validate CustomerCode
        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        // Validate city
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        // Load profile
        var profile = await _context.ShopOwnerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        // Handle ShopData updates
        string? newImageUrl = null;
        if (request.ShopImage != null)
        {
            using var imageStream = request.ShopImage.OpenReadStream();
            var imageResult = await _fileStorageService.UploadAsync(imageStream, request.ShopImage.FileName, "shops", ct);
            if (imageResult.IsFailure)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ImageUploadFailed);
            newImageUrl = imageResult.Value;
        }

        // Validate VAT/CRN uniqueness (exclude current ShopData)
        if (!string.IsNullOrEmpty(request.VAT))
        {
            var vatInUse = await _context.ShopData
                .AnyAsync(sd => sd.VAT == request.VAT && sd.CustomerCode != request.CustomerCode, ct);
            if (vatInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.VatAlreadyExists);
        }

        if (!string.IsNullOrEmpty(request.CRN))
        {
            var crnInUse = await _context.ShopData
                .AnyAsync(sd => sd.CRN == request.CRN && sd.CustomerCode != request.CustomerCode, ct);
            if (crnInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CrnAlreadyExists);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            // Update user
            user.Name = request.OwnerName;
            user.MobileNumber = request.MobileNumber;
            user.UserName = request.MobileNumber;
            user.PhoneNumber = request.MobileNumber;

            // Update city and salesman assignment
            if (user.NationalAddress == null)
                user.NationalAddress = new NationalAddress();

            user.NationalAddress.CityId = request.CityId;
            user.AssignedSalesManId = city.ApprovalSalesManId;

            if (request.NationalAddress != null)
            {
                user.NationalAddress.Street = request.NationalAddress.Street;
                user.NationalAddress.BuildingNumber = request.NationalAddress.BuildingNumber;
                user.NationalAddress.PostalCode = request.NationalAddress.PostalCode;
                user.NationalAddress.SubNumber = request.NationalAddress.SubNumber;
            }

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to update ShopOwner {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
            }

            // Update profile CustomerCode if changed
            if (profile != null && profile.CustomerCode != request.CustomerCode)
            {
                profile.CustomerCode = request.CustomerCode;
                profile.UpdatedBy = adminUserId;
                profile.UpdatedAt = DateTime.UtcNow;
            }

            // Update ShopData
            var shopData = await _context.ShopData
                .FirstOrDefaultAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

            if (shopData != null)
            {
                if (!string.IsNullOrEmpty(request.StoreName))
                    shopData.StoreName = request.StoreName;
                if (!string.IsNullOrEmpty(request.VAT))
                    shopData.VAT = request.VAT;
                if (!string.IsNullOrEmpty(request.CRN))
                    shopData.CRN = request.CRN;
                if (newImageUrl != null)
                    shopData.ShopImageUrl = newImageUrl;
                if (request.NationalAddress != null)
                {
                    shopData.CityId = request.CityId;
                    shopData.Street = request.NationalAddress.Street;
                    shopData.BuildingNumber = request.NationalAddress.BuildingNumber;
                    shopData.PostalCode = request.NationalAddress.PostalCode;
                    shopData.SubNumber = request.NationalAddress.SubNumber;
                }
                shopData.UpdatedBy = adminUserId;
                shopData.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} updated ShopOwner {UserId}", adminUserId, userId);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.ShopOwner, "تم تعديل صاحب المحل بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to update ShopOwner {UserId}", userId);
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> EditSellerAsync(
        string userId, AdminEditSellerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.Seller)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        if (user.MobileNumber != request.MobileNumber)
        {
            var mobileInUse = await _userRepository.Query()
                .AnyAsync(u => u.MobileNumber == request.MobileNumber && u.Id != userId, ct);
            if (mobileInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyInUse);
        }

        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        var profile = await _context.SellerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        string? newImageUrl = null;
        if (request.ShopImage != null)
        {
            using var imageStream = request.ShopImage.OpenReadStream();
            var imageResult = await _fileStorageService.UploadAsync(imageStream, request.ShopImage.FileName, "shops", ct);
            if (imageResult.IsFailure)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ImageUploadFailed);
            newImageUrl = imageResult.Value;
        }

        if (!string.IsNullOrEmpty(request.VAT))
        {
            var vatInUse = await _context.ShopData
                .AnyAsync(sd => sd.VAT == request.VAT && sd.CustomerCode != request.CustomerCode, ct);
            if (vatInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.VatAlreadyExists);
        }

        if (!string.IsNullOrEmpty(request.CRN))
        {
            var crnInUse = await _context.ShopData
                .AnyAsync(sd => sd.CRN == request.CRN && sd.CustomerCode != request.CustomerCode, ct);
            if (crnInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CrnAlreadyExists);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            user.Name = request.Name;
            user.MobileNumber = request.MobileNumber;
            user.UserName = request.MobileNumber;
            user.PhoneNumber = request.MobileNumber;

            if (user.NationalAddress == null)
                user.NationalAddress = new NationalAddress();

            user.NationalAddress.CityId = request.CityId;
            user.AssignedSalesManId = city.ApprovalSalesManId;

            if (request.NationalAddress != null)
            {
                user.NationalAddress.Street = request.NationalAddress.Street;
                user.NationalAddress.BuildingNumber = request.NationalAddress.BuildingNumber;
                user.NationalAddress.PostalCode = request.NationalAddress.PostalCode;
                user.NationalAddress.SubNumber = request.NationalAddress.SubNumber;
            }

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to update Seller {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
            }

            if (profile != null && profile.CustomerCode != request.CustomerCode)
            {
                profile.CustomerCode = request.CustomerCode;
                profile.UpdatedBy = adminUserId;
                profile.UpdatedAt = DateTime.UtcNow;
            }

            var shopData = await _context.ShopData
                .FirstOrDefaultAsync(sd => sd.CustomerCode == request.CustomerCode, ct);

            if (shopData != null)
            {
                if (!string.IsNullOrEmpty(request.StoreName))
                    shopData.StoreName = request.StoreName;
                if (!string.IsNullOrEmpty(request.VAT))
                    shopData.VAT = request.VAT;
                if (!string.IsNullOrEmpty(request.CRN))
                    shopData.CRN = request.CRN;
                if (newImageUrl != null)
                    shopData.ShopImageUrl = newImageUrl;
                if (request.NationalAddress != null)
                {
                    shopData.CityId = request.CityId;
                    shopData.Street = request.NationalAddress.Street;
                    shopData.BuildingNumber = request.NationalAddress.BuildingNumber;
                    shopData.PostalCode = request.NationalAddress.PostalCode;
                    shopData.SubNumber = request.NationalAddress.SubNumber;
                }
                shopData.UpdatedBy = adminUserId;
                shopData.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} updated Seller {UserId}", adminUserId, userId);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.Seller, "تم تعديل البائع بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to update Seller {UserId}", userId);
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }
    }

    public async Task<Result<AdminAddUserResponse>> EditTechnicianAsync(
        string userId, AdminEditTechnicianRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.Technician)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        if (user.MobileNumber != request.MobileNumber)
        {
            var mobileInUse = await _userRepository.Query()
                .AnyAsync(u => u.MobileNumber == request.MobileNumber && u.Id != userId, ct);
            if (mobileInUse)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyInUse);
        }

        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId && c.IsActive && !c.IsDeleted, ct);
        if (city == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityNotFound);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            user.Name = request.Name;
            user.MobileNumber = request.MobileNumber;
            user.UserName = request.MobileNumber;
            user.PhoneNumber = request.MobileNumber;

            if (user.NationalAddress == null)
                user.NationalAddress = new NationalAddress();

            user.NationalAddress.CityId = request.CityId;
            user.NationalAddress.PostalCode = request.PostalCode;
            user.AssignedSalesManId = city.ApprovalSalesManId;

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to update Technician {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} updated Technician {UserId}", adminUserId, userId);

            return Result.Success(new AdminAddUserResponse(
                user.Id, user.Name, user.MobileNumber,
                UserType.Technician, "تم تعديل الفني بنجاح"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to update Technician {UserId}", userId);
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }
    }

    #endregion
}
