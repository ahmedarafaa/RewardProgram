using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Admin.RewardSettings;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/reward-settings")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminRewardSettingsController : ControllerBase
{
    private readonly IAdminRewardSettingsService _settingsService;

    public AdminRewardSettingsController(IAdminRewardSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    [HasPermission(AdminPermissions.SettingsView)]
    [ProducesResponseType(typeof(RewardSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var result = await _settingsService.GetSettingsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut]
    [HasPermission(AdminPermissions.SettingsManage)]
    [ProducesResponseType(typeof(RewardSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateRewardSettingsRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _settingsService.UpdateSettingsAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
