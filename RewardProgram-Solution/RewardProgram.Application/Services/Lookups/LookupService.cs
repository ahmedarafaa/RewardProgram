using RewardProgram.Application.Contracts.Lookups;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Services.Lookups;

public class LookupService(IApplicationDbContext context) : ILookupService
{
    private readonly IApplicationDbContext _context = context;

    public Task<List<CityResponse>> GetCitiesAsync()
    {
        throw new NotImplementedException();
    }

    public Task<DistrictResponse?> GetDistrictByIdAsync(int districtId)
    {
        throw new NotImplementedException();
    }

    public Task<List<DistrictResponse>> GetDistrictsByCityAsync(int cityId)
    {
        throw new NotImplementedException();
    }
}
