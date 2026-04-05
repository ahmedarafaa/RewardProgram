using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Redemption;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/redemption")]
[Authorize(Roles = $"{UserRoles.Seller},{UserRoles.Technician}")]
public class RedemptionController : ControllerBase
{
    private readonly IRedemptionService _redemptionService;

    public RedemptionController(IRedemptionService redemptionService)
    {
        _redemptionService = redemptionService;
    }

    [HttpPost("request")]
    [ProducesResponseType(typeof(RedemptionRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRequest([FromBody] CreateRedemptionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _redemptionService.CreateRequestAsync(request, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(RedemptionRequestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveRequest(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _redemptionService.GetActiveRequestAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(PaginatedResult<RedemptionRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] RedemptionListQuery query, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _redemptionService.GetHistoryAsync(userId, query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("available-balance")]
    [ProducesResponseType(typeof(AvailableBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableBalance(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _redemptionService.GetAvailableBalanceAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
