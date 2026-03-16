using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Dashboard;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = $"{UserRoles.ShopOwner},{UserRoles.Seller},{UserRoles.Technician}")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _dashboardService.GetDashboardAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
