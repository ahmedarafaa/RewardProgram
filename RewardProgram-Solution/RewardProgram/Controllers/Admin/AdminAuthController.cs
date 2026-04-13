using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.API;
using RewardProgram.Application.Contracts.Auth;
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

    public AdminAuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IAuthService authService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _authService = authService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        if (user is null || user.IsDisabled)
            return Unauthorized(new ProblemDetails
            {
                Title = "اسم المستخدم أو كلمة المرور غير صحيحة",
                Status = 401
            });

        if (await _userManager.IsLockedOutAsync(user))
            return Unauthorized(new ProblemDetails
            {
                Title = "تم قفل الحساب مؤقتاً بسبب محاولات فاشلة متكررة. حاول لاحقاً.",
                Status = 401
            });

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new ProblemDetails
            {
                Title = "اسم المستخدم أو كلمة المرور غير صحيحة",
                Status = 401
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(UserRoles.SystemAdmin))
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new ProblemDetails
            {
                Title = "غير مصرح لك بالدخول",
                Status = 401
            });
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

    [Authorize(Roles = UserRoles.SystemAdmin)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _authService.RevokeTokenAsync(request.RefreshToken, userId, ct);
        return result.IsSuccess
            ? Ok(new { message = "تم تسجيل الخروج بنجاح" })
            : result.ToProblem();
    }
}

public record AdminLoginRequest(string Username, string Password);
