using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Lookups;

namespace RewardProgram.Application.Interfaces;

public interface ILookupService
{
    Task<List<CityResponse>> GetCitiesAsync();
    Task<Result<List<DistrictResponse>>> GetDistrictsByCityAsync(string cityId);
    Task<DistrictResponse?> GetDistrictByIdAsync(string districtId);
}
