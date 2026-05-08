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
            .Where(e => e.ShortAddress != null && e.ShopData != null);

        if (!string.IsNullOrWhiteSpace(cityId))
            query = query.Where(e => e.ShopData!.CityId == cityId);

        var rows = await query
            .OrderBy(e => e.CustomerName)
            .Select(e => new ShopMapItemResponse(
                e.CustomerName,
                e.ShopData!.ShopImageUrl,
                e.ShopData.City != null ? e.ShopData.City.NameAr : "",
                e.ShortAddress!,
                e.ShopData.Street,
                e.ShopData.District,
                e.ShopData.BuildingNumber))
            .ToListAsync(ct);

        return rows;
    }
}
