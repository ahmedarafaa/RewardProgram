using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.API;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IAuthService _authService;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public AdminAuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IAuthService authService,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _authService = authService;
        _localizer = localizer;
    }

    private ObjectResult Unauthorized401(string key) =>
        Unauthorized(new ProblemDetails
        {
            Title = _localizer[key].Value,
            Status = 401
        });

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        if (user is null || user.IsDisabled)
            return Unauthorized401("AdminAuth.InvalidCredentials");

        if (await _userManager.IsLockedOutAsync(user))
            return Unauthorized401("AdminAuth.AccountLocked");

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized401("AdminAuth.InvalidCredentials");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(UserRoles.SystemAdmin) && !roles.Contains(UserRoles.Admin))
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized401("AdminAuth.NotAuthorized");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var authResponse = await _tokenService.GenerateAdminAuthResponseAsync(user);
        return Ok(authResponse);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshAdminTokenAsync(request.RefreshToken, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [Authorize(Roles = UserRoles.AdminDashboard)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _authService.RevokeTokenAsync(request.RefreshToken, userId, ct);
        return result.IsSuccess
            ? Ok(new { message = _localizer["AdminAuth.LoggedOut"].Value })
            : result.ToProblem();
    }
}

public record AdminLoginRequest(string Username, string Password);
