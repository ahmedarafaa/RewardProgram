using RewardProgram.Application.Contracts.Lookups;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces;

public interface ILookupService
{
    Task<List<CityResponse>> GetCitiesAsync();
    Task<List<DistrictResponse>> GetDistrictsByCityAsync(int cityId);
    Task<DistrictResponse?> GetDistrictByIdAsync(int districtId);
}