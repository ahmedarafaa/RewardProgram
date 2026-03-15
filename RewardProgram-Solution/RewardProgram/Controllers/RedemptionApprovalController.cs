using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Redemption;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/redemption-approvals")]
[Authorize(Roles = $"{UserRoles.SalesMan},{UserRoles.ZoneManager},{UserRoles.SystemAdmin}")]
public class RedemptionApprovalController : ControllerBase
{
    private readonly IRedemptionApprovalService _approvalService;

    public RedemptionApprovalController(IRedemptionApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(PaginatedResult<PendingRedemptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.GetPendingAsync(approverId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("approve")]
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

    [HttpPost("confirm-cash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmCashHandover([FromBody] ConfirmCashHandoverRequest request, CancellationToken ct)
    {
        var handoverById = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _approvalService.ConfirmCashHandoverAsync(request, handoverById, ct);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }
}
