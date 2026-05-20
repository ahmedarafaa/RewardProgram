using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Redemptions;
using RewardProgram.Application.Contracts.Redemption;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/redemptions")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminRedemptionController : ControllerBase
{
    private readonly IAdminRedemptionService _adminRedemptionService;
    private readonly IRedemptionApprovalService _approvalService;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminRedemptionController(
        IAdminRedemptionService adminRedemptionService,
        IRedemptionApprovalService approvalService,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _adminRedemptionService = adminRedemptionService;
        _approvalService = approvalService;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    [HttpGet]
    [HasPermission(AdminPermissions.RedemptionsView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminRedemptionListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminRedemptionListQuery query,
        [FromQuery] string? export,
        CancellationToken ct)
    {
        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _adminRedemptionService.ExportAllAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.Redemptions"].Value, "redemptions", BuildRedemptionExportColumns(), ct);
        }

        var result = await _adminRedemptionService.GetAllAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminRedemptionListItemResponse>> BuildRedemptionExportColumns() =>
    [
        new(_l["Export.Header.CreatedAt"].Value, r => r.CreatedAt),
        new(_l["Export.Header.User"].Value, r => r.UserFullName),
        new(_l["Export.Header.Mobile"].Value, r => r.UserMobile),
        new(_l["Export.Header.Method"].Value, r => LocalizedEnum.Display(r.Method, _l)),
        new(_l["Export.Header.Status"].Value, r => LocalizedEnum.Display(r.Status, _l)),
        new(_l["Export.Header.Points"].Value, r => r.PointsAmount),
        new(_l["Export.Header.Sar"].Value, r => r.SarAmount),
    ];

    [HttpGet("{id}")]
    [HasPermission(AdminPermissions.RedemptionsView)]
    [ProducesResponseType(typeof(AdminRedemptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await _adminRedemptionService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("approve")]
    [HasPermission(AdminPermissions.RedemptionsManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve([FromBody] ApproveRedemptionRequest request, CancellationToken ct)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.ApproveAsync(request, approverId, ct);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    [HttpPost("reject")]
    [HasPermission(AdminPermissions.RedemptionsManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject([FromBody] RejectRedemptionRequest request, CancellationToken ct)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.RejectAsync(request, approverId, ct);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }
}
