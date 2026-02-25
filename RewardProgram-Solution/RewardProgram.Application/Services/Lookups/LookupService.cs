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

    private const string RegionsCacheKey = "Lookup_Regions";
    private const string CitiesByRegionCacheKeyPrefix = "Lookup_Cities_Region_";
    private const string AllCitiesCacheKey = "Lookup_AllCities";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public LookupService(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<RegionResponse>> GetRegionsAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(RegionsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Regions
                .Where(r => r.IsActive)
                .OrderBy(r => r.NameAr)
                .Select(r => new RegionResponse(
                    r.Id,
                    r.NameAr,
                    r.NameEn
                ))
                .ToListAsync(ct);
        }) ?? [];
    }

    public async Task<Result<List<CityResponse>>> GetCitiesByRegionAsync(string regionId, CancellationToken ct = default)
    {
        // Validate region exists
        var regions = await GetRegionsAsync(ct);
        if (!regions.Any(r => r.Id == regionId))
            return Result.Failure<List<CityResponse>>(LookupErrors.RegionNotFound);

        var cacheKey = $"{CitiesByRegionCacheKeyPrefix}{regionId}";

        var cities = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Cities
                .Where(c => c.RegionId == regionId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new CityResponse(
                    c.Id,
                    c.NameAr,
                    c.NameEn,
                    c.RegionId
                ))
                .ToListAsync(ct);
        }) ?? [];

        return Result.Success(cities);
    }

    public async Task<List<CityResponse>> GetAllCitiesAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(AllCitiesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Cities
                .Where(c => c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new CityResponse(
                    c.Id,
                    c.NameAr,
                    c.NameEn,
                    c.RegionId
                ))
                .ToListAsync(ct);
        }) ?? [];
    }
}
