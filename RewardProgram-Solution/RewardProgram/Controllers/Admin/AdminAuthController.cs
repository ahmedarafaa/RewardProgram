using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    public AdminAuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
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

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
            return Unauthorized(new ProblemDetails
            {
                Title = "اسم المستخدم أو كلمة المرور غير صحيحة",
                Status = 401
            });

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(UserRoles.SystemAdmin))
            return Unauthorized(new ProblemDetails
            {
                Title = "غير مصرح لك بالدخول",
                Status = 401
            });

        var authResponse = await _tokenService.GenerateAuthResponseAsync(user);
        return Ok(authResponse);
    }
}

public record AdminLoginRequest(string Username, string Password);
