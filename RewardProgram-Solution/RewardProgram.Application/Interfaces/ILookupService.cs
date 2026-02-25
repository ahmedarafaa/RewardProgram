using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Lookups;

namespace RewardProgram.Application.Interfaces;

public interface ILookupService
{
    Task<List<RegionResponse>> GetRegionsAsync(CancellationToken ct = default);
    Task<Result<List<CityResponse>>> GetCitiesByRegionAsync(string regionId, CancellationToken ct = default);
    Task<List<CityResponse>> GetAllCitiesAsync(CancellationToken ct = default);
    Task<Result<CustomerShopDataStatusResponse>> GetCustomerShopDataStatusAsync(string customerCode, CancellationToken ct = default);
}
