using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Scan;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/scan")]
[Authorize(Roles = $"{UserRoles.Seller},{UserRoles.Technician}")]
public class ScanController : ControllerBase
{
    private readonly IScanService _scanService;

    public ScanController(IScanService scanService)
    {
        _scanService = scanService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ScanBarcodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScanBarcode([FromBody] ScanBarcodeRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _scanService.ScanBarcodeAsync(request, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(PaginatedResult<ScanHistoryItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScanHistory([FromQuery] ScanHistoryQuery query, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _scanService.GetScanHistoryAsync(userId, query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
