using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Interfaces;
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
    private readonly IBarcodePdfGenerator _pdfGenerator;

    public AdminBarcodeController(IAdminBarcodeService barcodeService, IBarcodePdfGenerator pdfGenerator)
    {
        _barcodeService = barcodeService;
        _pdfGenerator = pdfGenerator;
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateBarcodes([FromBody] AdminGenerateBarcodesRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _barcodeService.GenerateBarcodesAsync(request, adminId, ct);

        if (!result.IsSuccess)
            return result.ToProblem();

        var data = result.Value;
        var pdfBytes = _pdfGenerator.GeneratePdf(data.ProductName, data.ProductCode, data.Codes);
        var fileName = $"barcodes-{data.ProductCode}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminBarcodeListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBarcodes([FromQuery] AdminBarcodeListQuery query, CancellationToken ct)
    {
        var result = await _barcodeService.ListBarcodesAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
