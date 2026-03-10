using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Scans;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/scans")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminScanController : ControllerBase
{
    private readonly IAdminBarcodeService _barcodeService;

    public AdminScanController(IAdminBarcodeService barcodeService)
    {
        _barcodeService = barcodeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminScanListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListScans([FromQuery] AdminScanListQuery query, CancellationToken ct)
    {
        var result = await _barcodeService.GetAdminScanListAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
