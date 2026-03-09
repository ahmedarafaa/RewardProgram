using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/barcodes")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminBarcodeController : ControllerBase
{
    private readonly IAdminBarcodeService _barcodeService;

    public AdminBarcodeController(IAdminBarcodeService barcodeService)
    {
        _barcodeService = barcodeService;
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(AdminGenerateBarcodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateBarcodes([FromBody] AdminGenerateBarcodesRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _barcodeService.GenerateBarcodesAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminBarcodeListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBarcodes([FromQuery] AdminBarcodeListQuery query, CancellationToken ct)
    {
        var result = await _barcodeService.ListBarcodesAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
