using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Notifications;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminNotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminNotificationController(
        INotificationService notificationService,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _notificationService = notificationService;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    [HttpPost("send")]
    [HasPermission(AdminPermissions.NotificationsManage)]
    [ProducesResponseType(typeof(AdminSendNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendNotification([FromBody] AdminSendNotificationRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!string.IsNullOrEmpty(request.TargetUserId))
        {
            var result = await _notificationService.SendToUserAsync(request.TargetUserId, request.Title, request.Body, adminId, ct);
            return result.IsSuccess
                ? Ok(new AdminSendNotificationResponse(1))
                : result.ToProblem();
        }

        if (!string.IsNullOrEmpty(request.RoleName))
        {
            var result = await _notificationService.SendToRoleAsync(request.RoleName, request.Title, request.Body, adminId, ct);
            return result.IsSuccess
                ? Ok(new AdminSendNotificationResponse(result.Value))
                : result.ToProblem();
        }

        // Broadcast to all
        var broadcastResult = await _notificationService.BroadcastAsync(request.Title, request.Body, adminId, ct);
        return broadcastResult.IsSuccess
            ? Ok(new AdminSendNotificationResponse(broadcastResult.Value))
            : broadcastResult.ToProblem();
    }

    [HttpGet]
    [HasPermission(AdminPermissions.NotificationsView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminNotificationHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetNotificationHistory(
        [FromQuery] AdminNotificationListQuery query,
        [FromQuery] string? export,
        CancellationToken ct)
    {
        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _notificationService.ExportNotificationHistoryAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.Notifications"].Value, "notifications", BuildNotificationExportColumns(), ct);
        }

        var result = await _notificationService.GetNotificationHistoryAsync(query, ct);
        return Ok(result);
    }

    private IReadOnlyList<ExcelColumn<AdminNotificationHistoryItem>> BuildNotificationExportColumns() =>
    [
        new(_l["Export.Header.SentAt"].Value, n => n.CreatedAt),
        new(_l["Export.Header.Recipient"].Value, n => n.UserName),
        new(_l["Export.Header.Type"].Value, n => LocalizedEnum.Display(n.Type, _l)),
        new(_l["Export.Header.Title"].Value, n => n.Title),
        new(_l["Export.Header.Body"].Value, n => n.Body),
        new(_l["Export.Header.Reference"].Value, n => n.ReferenceId),
        new(_l["Export.Header.Read"].Value, n => n.IsRead),
    ];
}
