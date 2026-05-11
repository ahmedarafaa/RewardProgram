using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Enums.UserEnums;
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

    #region Add User

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

    // ShopOwner / Seller / Technician are NOT admin-created — they self-register
    // through the public OTP-first flow (POST /api/auth/register/*). Endpoints
    // for those user types were removed per business owner directive 2026-05-11.

    #endregion

    #region List Users

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminUserListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? search,
        [FromQuery] UserType? userType,
        [FromQuery] RegistrationStatus? registrationStatus,
        [FromQuery] string? regionId,
        [FromQuery] bool? isDisabled,
        [FromQuery] bool? isDeleted,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new AdminUserListQuery(search, userType, registrationStatus, regionId, isDisabled, isDeleted, page, pageSize);
        var result = await _adminUserService.ListUsersAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(string id, CancellationToken ct)
    {
        var result = await _adminUserService.GetUserByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Toggle Status

    [HttpPatch("{id}/toggle-status")]
    [ProducesResponseType(typeof(AdminToggleStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ToggleStatus(string id, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.ToggleStatusAsync(id, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Edit User

    [HttpPut("salesman/{id}")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditSalesMan(string id, [FromBody] AdminEditSalesManRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.EditSalesManAsync(id, request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("zone-manager/{id}")]
    [ProducesResponseType(typeof(AdminAddUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditZoneManager(string id, [FromBody] AdminEditZoneManagerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.EditZoneManagerAsync(id, request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    // ShopOwner / Seller / Technician edit endpoints removed per owner directive
    // 2026-05-11. Those users update their own profile via /api/profile.

    #endregion

    #region Reassign

    [HttpPost("cities/reassign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReassignCities([FromBody] AdminReassignCitiesRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.ReassignCitiesAsync(request, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPost("regions/reassign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReassignRegion([FromBody] AdminReassignRegionRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.ReassignRegionAsync(request, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    #endregion

    #region Delete SM/ZM

    [HttpDelete("salesman/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSalesMan(string id, [FromBody] AdminDeleteSalesManRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.DeleteSalesManAsync(id, request, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpDelete("zone-manager/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteZoneManager(string id, [FromBody] AdminDeleteZoneManagerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.DeleteZoneManagerAsync(id, request, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    #endregion

    #region Restore Account

    [HttpPost("{id}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreUser(string id, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _adminUserService.RestoreUserAsync(id, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    #endregion
}
