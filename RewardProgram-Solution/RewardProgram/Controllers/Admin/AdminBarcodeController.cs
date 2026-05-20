using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/barcodes")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminBarcodeController : ControllerBase
{
    private readonly IAdminBarcodeService _barcodeService;
    private readonly IBarcodePdfGenerator _pdfGenerator;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminBarcodeController(
        IAdminBarcodeService barcodeService,
        IBarcodePdfGenerator pdfGenerator,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _barcodeService = barcodeService;
        _pdfGenerator = pdfGenerator;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    [HttpPost("generate")]
    [HasPermission(AdminPermissions.BarcodesManage)]
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
    [HasPermission(AdminPermissions.BarcodesView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminBarcodeListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListBarcodes(
        [FromQuery] AdminBarcodeListQuery query,
        [FromQuery] string? export,
        CancellationToken ct)
    {
        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _barcodeService.ExportBarcodesAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.Barcodes"].Value, "barcodes", BuildBarcodeExportColumns(), ct);
        }

        var result = await _barcodeService.ListBarcodesAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminBarcodeListItemResponse>> BuildBarcodeExportColumns() =>
    [
        new(_l["Export.Header.BarcodeCode"].Value, b => b.Code),
        new(_l["Export.Header.Product"].Value, b => b.ProductName),
        new(_l["Export.Header.PointValue"].Value, b => b.PointValue),
        new(_l["Export.Header.Status"].Value, b => LocalizedEnum.Display(b.Status, _l)),
        new(_l["Export.Header.CreatedAt"].Value, b => b.CreatedAt),
    ];
}
