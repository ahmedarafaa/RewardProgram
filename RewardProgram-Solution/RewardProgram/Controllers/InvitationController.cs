using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Invitation;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/invitation")]
[Authorize(Roles = $"{UserRoles.ShopOwner},{UserRoles.Seller},{UserRoles.Technician}")]
public class InvitationController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(InvitationInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetInvitationInfo(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _invitationService.GetInvitationInfoAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
