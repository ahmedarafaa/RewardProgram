using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Admin.Notifications;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminNotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public AdminNotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("send")]
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
}
