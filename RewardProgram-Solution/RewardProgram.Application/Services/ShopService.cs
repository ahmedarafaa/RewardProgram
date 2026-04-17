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

        var shops = await query
            .Select(e => new ShopMapItemResponse(
                e.CustomerName,
                e.ShopData != null ? e.ShopData.ShopImageUrl : "",
                e.ShopData != null ? e.ShopData.City.NameAr : "",
                e.ShortAddress!
            ))
            .OrderBy(s => s.CustomerName)
            .ToListAsync(ct);

        return shops;
    }
}
