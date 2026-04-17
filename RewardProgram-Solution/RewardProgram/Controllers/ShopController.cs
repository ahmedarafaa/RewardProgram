using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Shop;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/shops")]
[Authorize]
public class ShopController : ControllerBase
{
    private readonly IShopService _shopService;

    public ShopController(IShopService shopService)
    {
        _shopService = shopService;
    }

    [HttpGet("map")]
    [ProducesResponseType(typeof(List<ShopMapItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShopMap(
        [FromQuery] string? cityId,
        CancellationToken ct)
    {
        var result = await _shopService.GetShopMapAsync(cityId, ct);
        return Ok(result);
    }
}
