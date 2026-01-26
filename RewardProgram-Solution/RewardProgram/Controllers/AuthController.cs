using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.DTOs.Auth;
using RewardProgram.Application.DTOs.Auth.Additional;
using RewardProgram.Application.DTOs.Auth.UsersDTO;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Services.Auth;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IAuthService authservice, ILogger<AuthController> logger) : ControllerBase
{
    private readonly IAuthService _authservice = authservice;
    private readonly ILogger<AuthController> _logger = logger;

    [HttpPost("register/shop-owner")]
    [ProducesResponseType(typeof(RegistrationResponse),StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RegistrationResponse),StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RegistrationResponse),StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterShopOwner([FromBody] RegisterShopOwnerRequest request)
    {
        var result = await _authservice.RegisterShopOwnerAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();

    }
    [HttpPost("register/seller")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSeller([FromBody] RegisterSellerRequest request)
    {
        var result = await _authservice.RegisterSellerAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();

    }


    [HttpPost("register/technician")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSeller([FromBody] RegisterTechnicianRequest request)
    {
        var result = await _authservice.RegisterTechnicianAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();

    }

    [HttpPost("register/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyRegistration([FromBody] VerifyOtpRequest request)
    {
        var result = await _authservice.VerifyRegistrationAsync(request);

        return result.IsSuccess
            ? Ok(new { Message = "تم إكمال التسجيل بنجاح، في انتظار موافقة الإدارة" })
            : result.ToProblem();
    }


    [HttpPost("login")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authservice.LoginAsync(request);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenResponse request)
    {
        var result = await _authservice.RefreshTokenAsync(request);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToProblem();
    }
    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenResponse request)
    {
        var result = await _authservice.RevokeTokenAsync(request);

        return result.IsSuccess
            ? Ok(new { Message = "تم تسجيل الخروج بنجاح" })
            : result.ToProblem();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _authservice.GetUserByIdAsync(userId);

        return user.IsSuccess
            ? Ok(user.Value)
            : user.ToProblem();
    }
}
