using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminUserController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUserController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpPost("salesman")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSalesMan([FromBody] AdminAddSalesManRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.AddSalesManAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("zone-manager")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddZoneManager([FromBody] AdminAddZoneManagerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.AddZoneManagerAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("shop-owner")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddShopOwner([FromForm] AdminAddShopOwnerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.AddShopOwnerAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("seller")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSeller([FromForm] AdminAddSellerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.AddSellerAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("technician")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddTechnician([FromBody] AdminAddTechnicianRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.AddTechnicianAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
