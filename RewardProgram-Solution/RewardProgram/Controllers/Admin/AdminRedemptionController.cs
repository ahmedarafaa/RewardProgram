using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Redemptions;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/redemptions")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminRedemptionController : ControllerBase
{
    private readonly IAdminRedemptionService _adminRedemptionService;

    public AdminRedemptionController(IAdminRedemptionService adminRedemptionService)
    {
        _adminRedemptionService = adminRedemptionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminRedemptionListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminRedemptionListQuery query, CancellationToken ct)
    {
        var result = await _adminRedemptionService.GetAllAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminRedemptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await _adminRedemptionService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
