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

    [HttpGet("regions")]
    [ProducesResponseType(typeof(List<RegionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegions(CancellationToken ct)
    {
        var regions = await _lookupService.GetRegionsAsync(ct);
        return Ok(regions);
    }

    [HttpGet("regions/{regionId}/cities")]
    [ProducesResponseType(typeof(List<CityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCitiesByRegion(string regionId, CancellationToken ct)
    {
        var result = await _lookupService.GetCitiesByRegionAsync(regionId, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }

    [HttpGet("cities")]
    [ProducesResponseType(typeof(List<CityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCities(CancellationToken ct)
    {
        var cities = await _lookupService.GetAllCitiesAsync(ct);
        return Ok(cities);
    }

    [HttpGet("customer/{customerCode}/shop-data-status")]
    [ProducesResponseType(typeof(CustomerShopDataStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerShopDataStatus(string customerCode, CancellationToken ct)
    {
        var result = await _lookupService.GetCustomerShopDataStatusAsync(customerCode, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }
}
