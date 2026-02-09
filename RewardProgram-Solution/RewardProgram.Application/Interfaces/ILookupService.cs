using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Lookups;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces;

public interface ILookupService
{
    Task<Result<List<CityResponse>>> GetCitiesAsync();
    Task<Result<List<DistrictResponse>>> GetDistrictsByCityAsync(string cityId);
    Task<DistrictResponse?> GetDistrictByIdAsync(string districtId);
}