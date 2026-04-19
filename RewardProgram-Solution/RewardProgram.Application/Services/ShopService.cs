using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Contracts.Shop;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Services;

public class ShopService : IShopService
{
    private readonly IApplicationDbContext _context;

    public ShopService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ShopMapItemResponse>> GetShopMapAsync(string? cityId, CancellationToken ct = default)
    {
        var query = _context.ErpCustomers
            .AsNoTracking()
            .Where(e => e.ShortAddress != null);

        if (!string.IsNullOrWhiteSpace(cityId))
            query = query.Where(e => e.ShopData != null && e.ShopData.CityId == cityId);

        var rows = await query
            .OrderBy(e => e.CustomerName)
            .Select(e => new
            {
                e.CustomerName,
                ShopImageUrl = e.ShopData != null ? e.ShopData.ShopImageUrl : "",
                CityName = e.ShopData != null && e.ShopData.City != null ? e.ShopData.City.NameAr : "",
                ShortAddress = e.ShortAddress!
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ShopMapItemResponse(r.CustomerName, r.ShopImageUrl, r.CityName, r.ShortAddress))
            .ToList();
    }
}
