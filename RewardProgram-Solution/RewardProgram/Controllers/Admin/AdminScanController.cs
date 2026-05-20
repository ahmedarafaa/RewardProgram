using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Scans;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/scans")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminScanController : ControllerBase
{
    private readonly IAdminBarcodeService _barcodeService;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminScanController(
        IAdminBarcodeService barcodeService,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _barcodeService = barcodeService;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    [HttpGet]
    [HasPermission(AdminPermissions.ScansView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminScanListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListScans(
        [FromQuery] AdminScanListQuery query,
        [FromQuery] string? export,
        CancellationToken ct)
    {
        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _barcodeService.ExportScansAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.Scans"].Value, "scans", BuildScanExportColumns(), ct);
        }

        var result = await _barcodeService.GetAdminScanListAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminScanListItemResponse>> BuildScanExportColumns() =>
    [
        new(_l["Export.Header.ScannedAt"].Value, s => s.ScannedAt),
        new(_l["Export.Header.User"].Value, s => s.UserName),
        new(_l["Export.Header.Mobile"].Value, s => s.UserMobile),
        new(_l["Export.Header.ScannerRole"].Value, s => LocalizedEnum.Display(s.ScannerRole, _l)),
        new(_l["Export.Header.BarcodeCode"].Value, s => s.BarcodeCode),
        new(_l["Export.Header.Product"].Value, s => s.ProductName),
        new(_l["Export.Header.ProductCode"].Value, s => s.ProductCode),
        new(_l["Export.Header.PointsAwarded"].Value, s => s.PointsAwarded),
        new(_l["Export.Header.BarcodeStatus"].Value, s => LocalizedEnum.Display(s.BarcodeStatus, _l)),
        new(_l["Export.Header.Latitude"].Value, s => s.Latitude),
        new(_l["Export.Header.Longitude"].Value, s => s.Longitude),
    ];

    [HttpPost("{scanId}/cancel")]
    [HasPermission(AdminPermissions.ScansManage)]
    [ProducesResponseType(typeof(AdminCancelScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelScan(string scanId, CancellationToken ct)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _barcodeService.CancelScanAsync(scanId, adminUserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
