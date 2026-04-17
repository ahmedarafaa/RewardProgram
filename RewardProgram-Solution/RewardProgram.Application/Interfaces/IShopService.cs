using RewardProgram.Application.Contracts.Shop;

namespace RewardProgram.Application.Interfaces;

public interface IShopService
{
    Task<List<ShopMapItemResponse>> GetShopMapAsync(string? cityId, CancellationToken ct = default);
}
