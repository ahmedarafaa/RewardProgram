using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Enums.UserEnums;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminUserController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminUserController(
        IAdminUserService adminUserService,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _adminUserService = adminUserService;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    #region Add User

    [HttpPost("salesman")]
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminUserListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? search,
        [FromQuery] UserType? userType,
        [FromQuery] RegistrationStatus? registrationStatus,
        [FromQuery] string? regionId,
        [FromQuery] bool? isDisabled,
        [FromQuery] bool? isDeleted,
        [FromQuery] string? export = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new AdminUserListQuery(search, userType, registrationStatus, regionId, isDisabled, isDeleted, page, pageSize);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _adminUserService.ExportUsersAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.Users"].Value, "users", BuildUserExportColumns(), ct);
        }

        var result = await _adminUserService.ListUsersAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminUserListItemResponse>> BuildUserExportColumns() =>
    [
        new(_l["Export.Header.CreatedAt"].Value, u => u.CreatedAt),
        new(_l["Export.Header.Name"].Value, u => u.Name),
        new(_l["Export.Header.Mobile"].Value, u => u.MobileNumber),
        new(_l["Export.Header.Type"].Value, u => LocalizedEnum.Display(u.UserType, _l)),
        new(_l["Export.Header.RegistrationStatus"].Value, u => LocalizedEnum.Display(u.RegistrationStatus, _l)),
        new(_l["Export.Header.Roles"].Value, u => string.Join(", ", u.Roles)),
        new(_l["Export.Header.Region"].Value, u => u.RegionName),
        new(_l["Export.Header.City"].Value, u => u.CityName),
        new(_l["Export.Header.CustomerCode"].Value, u => u.CustomerCode),
        new(_l["Export.Header.StoreName"].Value, u => u.StoreName),
        new(_l["Export.Header.IsDisabled"].Value, u => u.IsDisabled),
        new(_l["Export.Header.IsAccountDeleted"].Value, u => u.IsAccountDeleted),
    ];

    [HttpGet("{id}")]
    [HasPermission(AdminPermissions.UsersView)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
    [HasPermission(AdminPermissions.UsersManage)]
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
