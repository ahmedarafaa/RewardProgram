using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Lookups;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Services.Lookups;

public class LookupService : ILookupService
{
    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    private const string CitiesCacheKey = "Lookup_Cities";
    private const string DistrictsByCityCacheKeyPrefix = "Lookup_Districts_City_";
    private const string DistrictByIdCacheKeyPrefix = "Lookup_District_";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public LookupService(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CityResponse>> GetCitiesAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(CitiesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Cities
                .Where(c => c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new CityResponse(
                    c.Id,
                    c.NameAr,
                    c.NameEn
                ))
                .ToListAsync(ct);
        }) ?? [];
    }

    public async Task<Result<List<DistrictResponse>>> GetDistrictsByCityAsync(string cityId, CancellationToken ct = default)
    {
        // Validate city exists (reuse GetCitiesAsync to avoid cache duplication — P4 fix)
        var cities = await GetCitiesAsync(ct);

        if (!cities.Any(c => c.Id == cityId))
            return Result.Failure<List<DistrictResponse>>(LookupErrors.CityNotFound);

        var cacheKey = $"{DistrictsByCityCacheKeyPrefix}{cityId}";

        var districts = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Districts
                .Where(d => d.CityId == cityId && d.IsActive)
                .OrderBy(d => d.NameAr)
                .Select(d => new DistrictResponse(
                    d.Id,
                    d.NameAr,
                    d.NameEn,
                    d.CityId,
                    d.Zone
                ))
                .ToListAsync(ct);
        }) ?? [];

        return Result.Success(districts);
    }

    public async Task<DistrictResponse?> GetDistrictByIdAsync(string districtId, CancellationToken ct = default)
    {
        var cacheKey = $"{DistrictByIdCacheKeyPrefix}{districtId}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Districts
                .Where(d => d.Id == districtId && d.IsActive)
                .Select(d => new DistrictResponse(
                    d.Id,
                    d.NameAr,
                    d.NameEn,
                    d.CityId,
                    d.Zone
                ))
                .FirstOrDefaultAsync(ct);
        });
    }
}
