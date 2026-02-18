using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Lookups;

namespace RewardProgram.Application.Interfaces;

public interface ILookupService
{
    Task<List<RegionResponse>> GetRegionsAsync(CancellationToken ct = default);
    Task<Result<List<CityResponse>>> GetCitiesByRegionAsync(string regionId, CancellationToken ct = default);
    Task<Result<List<DistrictResponse>>> GetDistrictsByCityAsync(string cityId, CancellationToken ct = default);
    Task<DistrictResponse?> GetDistrictByIdAsync(string districtId, CancellationToken ct = default);
}
