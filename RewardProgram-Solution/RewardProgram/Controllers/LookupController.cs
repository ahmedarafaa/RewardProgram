using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Abstractions;
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
    public async Task<IActionResult> GetCities(CancellationToken ct)
    {
        var cities = await _lookupService.GetCitiesAsync(ct);
        return Ok(cities);
    }

    [HttpGet("cities/{cityId}/districts")]
    [ProducesResponseType(typeof(List<DistrictResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDistrictsByCity(string cityId, CancellationToken ct)
    {
        var result = await _lookupService.GetDistrictsByCityAsync(cityId, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }
}
