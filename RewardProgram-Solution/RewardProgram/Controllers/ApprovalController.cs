using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/approvals")]
[Authorize(Roles = $"{UserRoles.SalesMan},{UserRoles.ZoneManager}")]
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public ApprovalController(IApprovalService approvalService, IStringLocalizer<ErrorMessages> localizer)
    {
        _approvalService = approvalService;
        _localizer = localizer;
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(PaginatedResult<PendingUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingRequests(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.GetPendingRequestsAsync(approverId, search, page, pageSize, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }

    [HttpGet("list")]
    [ProducesResponseType(typeof(PaginatedResult<ApprovalListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] ApprovalListStatusFilter status = ApprovalListStatusFilter.All,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.GetListAsync(approverId, status, search, page, pageSize, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }

    [HttpPost("approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Approve([FromBody] ApproveRequest request, CancellationToken ct = default)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.ApproveAsync(request.UserId, approverId, ct);

        return result.IsSuccess
            ? Ok(new { message = _localizer["Approval.ApproveSuccess"].Value })
            : result.ToProblem();
    }

    [HttpPost("reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Reject([FromBody] RejectRequest request, CancellationToken ct = default)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.RejectAsync(request.UserId, request.Reason, approverId, ct);

        return result.IsSuccess
            ? Ok(new { message = _localizer["Approval.RejectSuccess"].Value })
            : result.ToProblem();
    }
}
