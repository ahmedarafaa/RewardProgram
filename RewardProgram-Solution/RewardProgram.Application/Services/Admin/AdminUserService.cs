using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using static RewardProgram.Application.Helpers.PaginationHelper;
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
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var cityIds = request.CityIds ?? [];
        List<Domain.Entities.Users.City> cities = [];

        if (cityIds.Count > 0)
        {
            cities = await _context.Cities
                .Where(c => cityIds.Contains(c.Id) && c.IsActive && !c.IsDeleted)
                .ToListAsync(ct);

            if (cities.Count != cityIds.Count)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.SomeCitiesNotFound);

            if (cities.Any(c => !string.IsNullOrEmpty(c.ApprovalSalesManId)))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CityAlreadyHasSalesMan);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.Name,
                MobileNumber = mobile,
                UserType = UserType.SalesMan,
                RegistrationStatus = RegistrationStatus.Approved
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.SalesMan, AdminUserErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(createRoleResult.Error);
            }

            foreach (var city in cities)
            {
                city.ApprovalSalesManId = user.Id;
                city.UpdatedBy = adminUserId;
                city.UpdatedAt = DateTime.UtcNow;
            }

            if (cities.Count > 0)
            {
                var assignedCityIds = cities.Select(c => c.Id).ToList();
                var usersInCities = await _userRepository.Query()
                    .Where(u => u.UserType != UserType.SalesMan
                        && u.UserType != UserType.ZoneManager
                        && u.UserType != UserType.SystemAdmin
                        && u.NationalAddress != null
                        && assignedCityIds.Contains(u.NationalAddress.CityId))
                    .ToListAsync(ct);

                foreach (var u in usersInCities)
                    u.AssignedSalesManId = user.Id;
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
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        Domain.Entities.Users.Region? region = null;

        if (!string.IsNullOrEmpty(request.RegionId))
        {
            region = await _context.Regions
                .FirstOrDefaultAsync(r => r.Id == request.RegionId && r.IsActive && !r.IsDeleted, ct);

            if (region == null)
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionNotFound);

            if (!string.IsNullOrEmpty(region.ZoneManagerId))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.RegionAlreadyHasZoneManager);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.Name,
                MobileNumber = mobile,
                UserType = UserType.ZoneManager,
                RegistrationStatus = RegistrationStatus.Approved
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.ZoneManager, AdminUserErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(createRoleResult.Error);
            }

            if (region != null)
            {
                region.ZoneManagerId = user.Id;
                region.UpdatedBy = adminUserId;
                region.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} created ZoneManager {UserId} for region {RegionId}",
                adminUserId, user.Id, region?.Id ?? "(unassigned)");

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
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        if (await _userRepository.MobileExistsAsync(mobile, ct))
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.MobileAlreadyExists);

        var erpExists = await _context.ErpCustomers
            .AnyAsync(e => e.CustomerCode == request.CustomerCode, ct);
        if (!erpExists)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeNotFound);

        // Reject if another ShopOwner already owns this CustomerCode
        var existingOwner = await _context.ShopOwnerProfiles
            .AnyAsync(p => p.CustomerCode == request.CustomerCode, ct);
        if (existingOwner)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.CustomerCodeAlreadyOwned);

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
                || request.NationalAddress == null || string.IsNullOrEmpty(request.ShortAddress))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ShopDataRequired);

            var uniqueCheck = await ShopDataValidationHelper.ValidateUniqueFieldsAsync(
                _context, request.VAT!, request.CRN!, request.ShortAddress!, ct: ct);
            if (uniqueCheck.IsFailure)
                return Result.Failure<AdminAddUserResponse>(uniqueCheck.Error);

            var imgResult = await FileUploadHelper.UploadShopImageAsync(request.ShopImage, _fileStorageService, ct);
            if (imgResult.IsFailure)
                return Result.Failure<AdminAddUserResponse>(imgResult.Error);

            shopImageUrl = imgResult.Value;
        }

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
                RegistrationStatus = RegistrationStatus.Approved,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = !shopDataExists && request.NationalAddress != null
                    ? new NationalAddress
                    {
                        CityId = request.CityId,
                        Street = request.NationalAddress.Street,
                        BuildingNumber = request.NationalAddress.BuildingNumber,
                        PostalCode = request.NationalAddress.PostalCode,
                        SubNumber = request.NationalAddress.SubNumber,
                        District = request.NationalAddress.District ?? string.Empty
                    }
                    : new NationalAddress { CityId = request.CityId }
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.ShopOwner, AdminUserErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(createRoleResult.Error);
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
                    ShortAddress = request.ShortAddress!,
                    District = request.NationalAddress?.District ?? string.Empty,
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
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        if (await _userRepository.MobileExistsAsync(mobile, ct))
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
                || request.NationalAddress == null || string.IsNullOrEmpty(request.ShortAddress))
                return Result.Failure<AdminAddUserResponse>(AdminUserErrors.ShopDataRequired);

            var uniqueCheck = await ShopDataValidationHelper.ValidateUniqueFieldsAsync(
                _context, request.VAT!, request.CRN!, request.ShortAddress!, ct: ct);
            if (uniqueCheck.IsFailure)
                return Result.Failure<AdminAddUserResponse>(uniqueCheck.Error);

            var imgResult = await FileUploadHelper.UploadShopImageAsync(request.ShopImage, _fileStorageService, ct);
            if (imgResult.IsFailure)
                return Result.Failure<AdminAddUserResponse>(imgResult.Error);

            shopImageUrl = imgResult.Value;
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var user = new ApplicationUser
            {
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.Name,
                MobileNumber = mobile,
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
                        SubNumber = request.NationalAddress.SubNumber,
                        District = request.NationalAddress.District ?? string.Empty
                    }
                    : new NationalAddress { CityId = request.CityId }
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.Seller, AdminUserErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(createRoleResult.Error);
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
                    ShortAddress = request.ShortAddress!,
                    District = request.NationalAddress?.District ?? string.Empty,
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
        var mobile = MobileNumberHelper.Normalize(request.MobileNumber);

        if (await _userRepository.MobileExistsAsync(mobile, ct))
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
                UserName = mobile,
                PhoneNumber = mobile,
                Name = request.Name,
                MobileNumber = mobile,
                UserType = UserType.Technician,
                RegistrationStatus = RegistrationStatus.Approved,
                AssignedSalesManId = city.ApprovalSalesManId,
                NationalAddress = new NationalAddress
                {
                    CityId = request.CityId,
                    PostalCode = request.PostalCode,
                    District = request.District
                }
            };

            var createRoleResult = await UserCreationHelper.CreateWithRoleAsync(
                _userRepository, user, UserRoles.Technician, AdminUserErrors.CreateUserFailed, _logger);
            if (createRoleResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<AdminAddUserResponse>(createRoleResult.Error);
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

        if (query.IsDeleted.HasValue)
            usersQuery = usersQuery.Where(u => u.IsAccountDeleted == query.IsDeleted.Value);

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

        var (page, pageSize) = Normalize(query.Page, query.PageSize);

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
                u.IsAccountDeleted,
                u.AccountDeletedAt,
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
                u.IsDisabled, u.IsAccountDeleted, u.AccountDeletedAt,
                u.CreatedAt, regionName, cityName, customerCode, storeName);
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

        user.Name = request.Name;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Admin: Failed to update SalesMan {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }

        _logger.LogInformation("Admin {AdminId} updated SalesMan {UserId}", adminUserId, userId);

        return Result.Success(new AdminAddUserResponse(
            user.Id, user.Name, user.MobileNumber,
            UserType.SalesMan, "تم تعديل مندوب المبيعات بنجاح"));
    }

    public async Task<Result<AdminAddUserResponse>> EditZoneManagerAsync(
        string userId, AdminEditZoneManagerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.ZoneManager)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        user.Name = request.Name;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Admin: Failed to update ZoneManager {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }

        _logger.LogInformation("Admin {AdminId} updated ZoneManager {UserId}", adminUserId, userId);

        return Result.Success(new AdminAddUserResponse(
            user.Id, user.Name, user.MobileNumber,
            UserType.ZoneManager, "تم تعديل مدير المنطقة بنجاح"));
    }

    public async Task<Result<AdminAddUserResponse>> EditShopOwnerAsync(
        string userId, AdminEditShopOwnerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.ShopOwner)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        user.Name = request.OwnerName;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Admin: Failed to update ShopOwner {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }

        _logger.LogInformation("Admin {AdminId} updated ShopOwner {UserId}", adminUserId, userId);

        return Result.Success(new AdminAddUserResponse(
            user.Id, user.Name, user.MobileNumber,
            UserType.ShopOwner, "تم تعديل صاحب المحل بنجاح"));
    }

    public async Task<Result<AdminAddUserResponse>> EditSellerAsync(
        string userId, AdminEditSellerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.Seller)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        user.Name = request.Name;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Admin: Failed to update Seller {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }

        _logger.LogInformation("Admin {AdminId} updated Seller {UserId}", adminUserId, userId);

        return Result.Success(new AdminAddUserResponse(
            user.Id, user.Name, user.MobileNumber,
            UserType.Seller, "تم تعديل البائع بنجاح"));
    }

    public async Task<Result<AdminAddUserResponse>> EditTechnicianAsync(
        string userId, AdminEditTechnicianRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.Technician)
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UserTypeMismatch);

        user.Name = request.Name;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Admin: Failed to update Technician {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminAddUserResponse>(AdminUserErrors.UpdateUserFailed);
        }

        _logger.LogInformation("Admin {AdminId} updated Technician {UserId}", adminUserId, userId);

        return Result.Success(new AdminAddUserResponse(
            user.Id, user.Name, user.MobileNumber,
            UserType.Technician, "تم تعديل الفني بنجاح"));
    }

    #endregion

    #region Reassign

    public async Task<Result> ReassignCitiesAsync(
        AdminReassignCitiesRequest request, string adminUserId, CancellationToken ct = default)
    {
        var toSalesMan = await _userRepository.FindByIdAsync(request.ToSalesManId, ct);
        if (toSalesMan == null)
            return Result.Failure(AdminUserErrors.UserNotFound);

        if (toSalesMan.UserType != UserType.SalesMan)
            return Result.Failure(AdminUserErrors.ReassignmentTargetNotSalesMan);

        var cities = await _context.Cities
            .Where(c => request.CityIds.Contains(c.Id) && c.IsActive && !c.IsDeleted)
            .ToListAsync(ct);

        if (cities.Count != request.CityIds.Count)
            return Result.Failure(AdminUserErrors.SomeCitiesNotFound);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            foreach (var city in cities)
            {
                city.ApprovalSalesManId = request.ToSalesManId;
                city.UpdatedBy = adminUserId;
                city.UpdatedAt = DateTime.UtcNow;
            }

            // Reassign all users in these cities to the new salesman
            var cityIds = cities.Select(c => c.Id).ToList();
            var usersInCities = await _userRepository.Query()
                .Where(u => u.UserType != UserType.SalesMan
                    && u.UserType != UserType.ZoneManager
                    && u.UserType != UserType.SystemAdmin
                    && u.NationalAddress != null
                    && cityIds.Contains(u.NationalAddress.CityId))
                .ToListAsync(ct);

            foreach (var u in usersInCities)
                u.AssignedSalesManId = request.ToSalesManId;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} reassigned {Count} cities to SalesMan {ToSalesManId}",
                adminUserId, cities.Count, request.ToSalesManId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to reassign cities to SalesMan {ToSalesManId}", request.ToSalesManId);
            return Result.Failure(AdminUserErrors.UpdateUserFailed);
        }
    }

    public async Task<Result> ReassignRegionAsync(
        AdminReassignRegionRequest request, string adminUserId, CancellationToken ct = default)
    {
        var toZoneManager = await _userRepository.FindByIdAsync(request.ToZoneManagerId, ct);
        if (toZoneManager == null)
            return Result.Failure(AdminUserErrors.UserNotFound);

        if (toZoneManager.UserType != UserType.ZoneManager)
            return Result.Failure(AdminUserErrors.ReassignmentTargetNotZoneManager);

        var region = await _context.Regions
            .FirstOrDefaultAsync(r => r.Id == request.RegionId && r.IsActive && !r.IsDeleted, ct);

        if (region == null)
            return Result.Failure(AdminUserErrors.RegionNotFound);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            region.ZoneManagerId = request.ToZoneManagerId;
            region.UpdatedBy = adminUserId;
            region.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} reassigned region {RegionId} to ZoneManager {ToZoneManagerId}",
                adminUserId, request.RegionId, request.ToZoneManagerId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to reassign region {RegionId}", request.RegionId);
            return Result.Failure(AdminUserErrors.UpdateUserFailed);
        }
    }

    #endregion

    #region Delete SM/ZM

    public async Task<Result> DeleteSalesManAsync(
        string userId, AdminDeleteSalesManRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.SalesMan)
            return Result.Failure(AdminUserErrors.UserTypeMismatch);

        // Load current cities owned by this SM
        var currentCities = await _context.Cities
            .Where(c => c.ApprovalSalesManId == userId)
            .ToListAsync(ct);

        var currentCityIds = currentCities.Select(c => c.Id).ToHashSet();
        var reassignmentMap = request.CityReassignments
            .ToDictionary(r => r.CityId, r => r.NewSalesManId);

        // Every current city must be reassigned
        if (currentCityIds.Count != reassignmentMap.Count
            || !currentCityIds.All(id => reassignmentMap.ContainsKey(id)))
            return Result.Failure(AdminUserErrors.AllCitiesMustBeReassigned);

        // No reassignment target can be this SM himself
        if (reassignmentMap.Values.Any(id => id == userId))
            return Result.Failure(AdminUserErrors.CityNotOwnedBySalesMan);

        // Validate all target SM IDs
        var targetIds = reassignmentMap.Values.Distinct().ToList();
        var targets = await _userRepository.Query()
            .Where(u => targetIds.Contains(u.Id))
            .ToListAsync(ct);

        if (targets.Count != targetIds.Count
            || targets.Any(u => u.UserType != UserType.SalesMan))
            return Result.Failure(AdminUserErrors.ReassignmentTargetNotSalesMan);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            foreach (var city in currentCities)
            {
                city.ApprovalSalesManId = reassignmentMap[city.Id];
                city.UpdatedBy = adminUserId;
                city.UpdatedAt = DateTime.UtcNow;
            }

            // Update AssignedSalesManId for users in these cities
            var usersInCities = await _userRepository.Query()
                .Where(u => u.UserType != UserType.SalesMan
                    && u.UserType != UserType.ZoneManager
                    && u.UserType != UserType.SystemAdmin
                    && u.NationalAddress != null
                    && currentCityIds.Contains(u.NationalAddress.CityId))
                .ToListAsync(ct);

            foreach (var u in usersInCities)
            {
                var cityId = u.NationalAddress!.CityId;
                u.AssignedSalesManId = reassignmentMap[cityId];
            }

            // Soft-delete the SM
            user.IsDisabled = true;
            user.IsAccountDeleted = true;
            user.AccountDeletedAt = DateTime.UtcNow;

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to soft-delete SalesMan {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure(AdminUserErrors.UpdateUserFailed);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} deleted SalesMan {UserId} (reassigned {Count} cities)",
                adminUserId, userId, currentCities.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to delete SalesMan {UserId}", userId);
            return Result.Failure(AdminUserErrors.UpdateUserFailed);
        }
    }

    public async Task<Result> DeleteZoneManagerAsync(
        string userId, AdminDeleteZoneManagerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(AdminUserErrors.UserNotFound);

        if (user.UserType != UserType.ZoneManager)
            return Result.Failure(AdminUserErrors.UserTypeMismatch);

        var managedRegion = await _context.Regions
            .FirstOrDefaultAsync(r => r.ZoneManagerId == userId, ct);

        if (managedRegion != null)
        {
            if (string.IsNullOrEmpty(request.NewZoneManagerId))
                return Result.Failure(AdminUserErrors.ReplacementZoneManagerRequired);

            if (request.NewZoneManagerId == userId)
                return Result.Failure(AdminUserErrors.ReassignmentTargetNotZoneManager);

            var replacement = await _userRepository.FindByIdAsync(request.NewZoneManagerId, ct);
            if (replacement == null)
                return Result.Failure(AdminUserErrors.UserNotFound);

            if (replacement.UserType != UserType.ZoneManager)
                return Result.Failure(AdminUserErrors.ReassignmentTargetNotZoneManager);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            if (managedRegion != null)
            {
                managedRegion.ZoneManagerId = request.NewZoneManagerId;
                managedRegion.UpdatedBy = adminUserId;
                managedRegion.UpdatedAt = DateTime.UtcNow;
            }

            user.IsDisabled = true;
            user.IsAccountDeleted = true;
            user.AccountDeletedAt = DateTime.UtcNow;

            var updateResult = await _userRepository.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Admin: Failed to soft-delete ZoneManager {UserId}: {Errors}",
                    userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync(ct);
                return Result.Failure(AdminUserErrors.UpdateUserFailed);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} deleted ZoneManager {UserId} (region {RegionId} → {NewZmId})",
                adminUserId, userId, managedRegion?.Id ?? "(none)", request.NewZoneManagerId ?? "(none)");

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to delete ZoneManager {UserId}", userId);
            return Result.Failure(AdminUserErrors.UpdateUserFailed);
        }
    }

    #endregion
}
