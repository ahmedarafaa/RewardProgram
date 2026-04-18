using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Enums.UserEnums;
using static RewardProgram.Application.Helpers.PaginationHelper;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IApplicationDbContext _context;
    private readonly IHostEnvironment _environment;

    public DevController(
        IUserRepository userRepository,
        IApplicationDbContext context,
        IHostEnvironment environment)
    {
        _userRepository = userRepository;
        _context = context;
        _environment = environment;
    }

    [HttpGet("seeded-users")]
    public async Task<IActionResult> GetSeededUsers(
        [FromQuery] string? id,
        [FromQuery] string? search,
        [FromQuery] UserType? userType,
        [FromQuery] RegistrationStatus? registrationStatus,
        [FromQuery] string? regionId,
        [FromQuery] string? cityId,
        [FromQuery] bool? isDisabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
            return NotFound();

        var (normalizedPage, normalizedPageSize) = Normalize(page, pageSize);

        var query = _userRepository.Query().AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(id))
            query = query.Where(u => u.Id == id);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.Name.Contains(search) || u.MobileNumber.Contains(search));

        if (userType.HasValue)
            query = query.Where(u => u.UserType == userType.Value);

        if (registrationStatus.HasValue)
            query = query.Where(u => u.RegistrationStatus == registrationStatus.Value);

        if (regionId != null)
            query = query.Where(u =>
                u.NationalAddress != null &&
                _context.Cities
                    .Where(c => c.RegionId == regionId)
                    .Select(c => c.Id)
                    .Contains(u.NationalAddress.CityId));

        if (cityId != null)
            query = query.Where(u =>
                u.NationalAddress != null && u.NationalAddress.CityId == cityId);

        if (isDisabled.HasValue)
            query = query.Where(u => u.IsDisabled == isDisabled.Value);

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.UserType)
            .ThenBy(u => u.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.MobileNumber,
                UserType = u.UserType.ToString(),
                RegistrationStatus = u.RegistrationStatus.ToString(),
                u.IsDisabled,
                u.CreatedAt,

                // Location
                CityId = u.NationalAddress != null ? u.NationalAddress.CityId : null,
                CityName = u.NationalAddress != null
                    ? _context.Cities
                        .Where(c => c.Id == u.NationalAddress.CityId)
                        .Select(c => c.NameAr)
                        .FirstOrDefault()
                    : null,
                RegionId = u.NationalAddress != null
                    ? _context.Cities
                        .Where(c => c.Id == u.NationalAddress.CityId)
                        .Select(c => c.RegionId)
                        .FirstOrDefault()
                    : null,
                RegionName = u.NationalAddress != null
                    ? _context.Cities
                        .Where(c => c.Id == u.NationalAddress.CityId)
                        .Join(_context.Regions, c => c.RegionId, r => r.Id, (c, r) => r.NameAr)
                        .FirstOrDefault()
                    : null,

                // Address details
                Street = u.NationalAddress != null ? u.NationalAddress.Street : null,
                BuildingNumber = u.NationalAddress != null ? (int?)u.NationalAddress.BuildingNumber : null,
                PostalCode = u.NationalAddress != null ? u.NationalAddress.PostalCode : null,
                SubNumber = u.NationalAddress != null ? (int?)u.NationalAddress.SubNumber : null,
                District = u.NationalAddress != null ? u.NationalAddress.District : null,

                // ShopOwner/Seller — CustomerCode + ShopData
                CustomerCode = u.UserType == UserType.ShopOwner
                    ? _context.ShopOwnerProfiles
                        .Where(p => p.UserId == u.Id)
                        .Select(p => p.CustomerCode)
                        .FirstOrDefault()
                    : u.UserType == UserType.Seller
                        ? _context.SellerProfiles
                            .Where(p => p.UserId == u.Id)
                            .Select(p => p.CustomerCode)
                            .FirstOrDefault()
                        : null,

                StoreName = u.UserType == UserType.ShopOwner || u.UserType == UserType.Seller
                    ? _context.ShopData
                        .Where(sd => sd.CustomerCode ==
                            (u.UserType == UserType.ShopOwner
                                ? _context.ShopOwnerProfiles.Where(p => p.UserId == u.Id).Select(p => p.CustomerCode).FirstOrDefault()
                                : _context.SellerProfiles.Where(p => p.UserId == u.Id).Select(p => p.CustomerCode).FirstOrDefault()))
                        .Select(sd => sd.StoreName)
                        .FirstOrDefault()
                    : null,

                // ZoneManager — managed region
                ManagedRegionId = u.UserType == UserType.ZoneManager
                    ? _context.Regions
                        .Where(r => r.ZoneManagerId == u.Id)
                        .Select(r => r.Id)
                        .FirstOrDefault()
                    : null,
                ManagedRegionName = u.UserType == UserType.ZoneManager
                    ? _context.Regions
                        .Where(r => r.ZoneManagerId == u.Id)
                        .Select(r => r.NameAr)
                        .FirstOrDefault()
                    : null,

                // SalesMan — assigned cities
                AssignedCities = u.UserType == UserType.SalesMan
                    ? _context.Cities
                        .Where(c => c.ApprovalSalesManId == u.Id)
                        .Select(c => new { c.Id, c.NameAr })
                        .ToList()
                    : null,

                // Invitation
                u.InvitationCode,
                u.InvitedByUserId
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Items = users,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)normalizedPageSize)
        });
    }

    [HttpGet("users/{userId}/cities")]
    public async Task<IActionResult> GetUserCities(string userId, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
            return NotFound();

        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { message = "User not found" });

        if (user.UserType != UserType.SalesMan)
            return BadRequest(new { message = "This endpoint is only available for SalesMan users" });

        var cities = await _context.Cities
            .Where(c => c.ApprovalSalesManId == userId)
            .Select(c => new
            {
                c.Id,
                c.NameAr,
                c.NameEn,
                c.RegionId,
                RegionName = _context.Regions
                    .Where(r => r.Id == c.RegionId)
                    .Select(r => r.NameAr)
                    .FirstOrDefault(),
                c.IsActive
            })
            .OrderBy(c => c.NameAr)
            .ToListAsync(ct);

        return Ok(new
        {
            UserId = user.Id,
            UserName = user.Name,
            UserType = user.UserType.ToString(),
            Count = cities.Count,
            Cities = cities
        });
    }

    [HttpGet("salesmen-and-zonemanagers")]
    public async Task<IActionResult> GetSalesmenAndZoneManagers(CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
            return NotFound();

        var users = await _userRepository.Query()
            .Where(u => u.UserType == UserType.SalesMan || u.UserType == UserType.ZoneManager)
            .OrderBy(u => u.UserType)
            .ThenBy(u => u.Name)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.MobileNumber,
                UserType = u.UserType.ToString(),
                RegistrationStatus = u.RegistrationStatus.ToString(),
                u.IsDisabled,

                AssignedCities = u.UserType == UserType.SalesMan
                    ? _context.Cities
                        .Where(c => c.ApprovalSalesManId == u.Id)
                        .Select(c => new { c.Id, c.NameAr })
                        .ToList()
                    : null,

                ManagedRegion = u.UserType == UserType.ZoneManager
                    ? _context.Regions
                        .Where(r => r.ZoneManagerId == u.Id)
                        .Select(r => new { r.Id, r.NameAr })
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);

        var salesmen = users.Where(u => u.UserType == nameof(UserType.SalesMan)).ToList();
        var zoneManagers = users.Where(u => u.UserType == nameof(UserType.ZoneManager)).ToList();

        return Ok(new
        {
            SalesMen = salesmen,
            ZoneManagers = zoneManagers,
            TotalSalesMen = salesmen.Count,
            TotalZoneManagers = zoneManagers.Count
        });
    }

    [HttpGet("shop-owners-with-sellers")]
    public async Task<IActionResult> GetShopOwnersWithSellers(
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
            return NotFound();

        var normalizedTake = take <= 0 ? 10 : Math.Min(take, 100);

        var sellerCustomerCodes = await _context.SellerProfiles
            .AsNoTracking()
            .Select(s => s.CustomerCode)
            .Distinct()
            .ToListAsync(ct);

        var shopOwners = await (
            from p in _context.ShopOwnerProfiles.AsNoTracking()
            join u in _userRepository.Query() on p.UserId equals u.Id
            where sellerCustomerCodes.Contains(p.CustomerCode)
            select new
            {
                ShopOwnerId = u.Id,
                ShopOwnerName = u.Name,
                u.MobileNumber,
                RegistrationStatus = u.RegistrationStatus.ToString(),
                u.IsDisabled,
                p.CustomerCode,
                StoreName = _context.ShopData
                    .Where(sd => sd.CustomerCode == p.CustomerCode)
                    .Select(sd => sd.StoreName)
                    .FirstOrDefault(),
                Sellers = (
                    from sp in _context.SellerProfiles
                    join su in _userRepository.Query() on sp.UserId equals su.Id
                    where sp.CustomerCode == p.CustomerCode
                    select new
                    {
                        su.Id,
                        su.Name,
                        su.MobileNumber,
                        RegistrationStatus = su.RegistrationStatus.ToString(),
                        su.IsDisabled
                    }).ToList()
            })
            .OrderBy(x => x.ShopOwnerName)
            .Take(normalizedTake)
            .ToListAsync(ct);

        return Ok(new
        {
            Items = shopOwners.Select(x => new
            {
                x.ShopOwnerId,
                x.ShopOwnerName,
                x.MobileNumber,
                x.RegistrationStatus,
                x.IsDisabled,
                x.CustomerCode,
                x.StoreName,
                TotalSellers = x.Sellers.Count,
                x.Sellers
            }),
            Count = shopOwners.Count
        });
    }

    [HttpGet("users/{userId}/regions")]
    public async Task<IActionResult> GetUserRegions(string userId, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
            return NotFound();

        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { message = "User not found" });

        if (user.UserType != UserType.ZoneManager)
            return BadRequest(new { message = "This endpoint is only available for ZoneManager users" });

        var regions = await _context.Regions
            .Where(r => r.ZoneManagerId == userId)
            .Select(r => new
            {
                r.Id,
                r.NameAr,
                r.NameEn,
                r.IsActive,
                CityCount = _context.Cities.Count(c => c.RegionId == r.Id)
            })
            .OrderBy(r => r.NameAr)
            .ToListAsync(ct);

        return Ok(new
        {
            UserId = user.Id,
            UserName = user.Name,
            UserType = user.UserType.ToString(),
            Count = regions.Count,
            Regions = regions
        });
    }
}
