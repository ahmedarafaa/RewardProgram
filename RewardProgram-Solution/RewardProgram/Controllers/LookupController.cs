using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Lookups;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/lookup")]
public class LookupController : ControllerBase
{
    private readonly ILookupService _lookupService;

    public LookupController(ILookupService lookupService)
    {
        _lookupService = lookupService;
    }

    [HttpGet("cities")]
    [ProducesResponseType(typeof(List<CityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCities()
    {
        var cities = await _lookupService.GetCitiesAsync();
        return Ok(cities);
    }

    [HttpGet("cities/{cityId}/districts")]
    [ProducesResponseType(typeof(List<DistrictResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistrictsByCity(string cityId)
    {
        var districts = await _lookupService.GetDistrictsByCityAsync(cityId);
        return Ok(districts);
    }
}
