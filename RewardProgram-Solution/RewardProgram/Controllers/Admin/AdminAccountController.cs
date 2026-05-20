using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Admin.Accounts;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Controllers.Admin;

/// <summary>
/// Admin-account management. SystemAdmin-only — creating, editing and deleting
/// admin accounts and assigning permissions is never delegated to granular admins.
/// </summary>
[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminAccountController : ControllerBase
{
    private readonly IAdminAccountService _service;

    public AdminAccountController(IAdminAccountService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AdminAccountListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _service.ListAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("permissions")]
    [ProducesResponseType(typeof(List<PermissionCatalogModule>), StatusCodes.Status200OK)]
    public IActionResult GetPermissionCatalog()
    {
        var result = _service.GetPermissionCatalog();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminAccountDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminAccountDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAdminAccountRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AdminAccountDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateAdminAccountRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}/permissions")]
    [ProducesResponseType(typeof(AdminAccountDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermissions(string id, [FromBody] SetAdminPermissionsRequest request, CancellationToken ct)
    {
        var result = await _service.SetPermissionsAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}
