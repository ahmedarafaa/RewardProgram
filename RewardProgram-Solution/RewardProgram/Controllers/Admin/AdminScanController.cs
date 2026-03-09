using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Scans;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/scans")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminScanController : ControllerBase
{
    private readonly IScanService _scanService;

    public AdminScanController(IScanService scanService)
    {
        _scanService = scanService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminScanListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListScans([FromQuery] AdminScanListQuery query, CancellationToken ct)
    {
        var result = await _scanService.GetAdminScanListAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
