using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Profile;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(Roles = $"{UserRoles.Seller},{UserRoles.Technician},{UserRoles.ShopOwner}")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _profileService.GetProfileAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("photo")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePhoto(IFormFile photo, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _profileService.UpdateProfilePhotoAsync(userId, photo, ct);
        return result.IsSuccess
            ? Ok(new { profileImageUrl = result.Value })
            : result.ToProblem();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _profileService.DeleteAccountAsync(userId, ct);
        return result.IsSuccess
            ? Ok(new { message = "تم حذف الحساب بنجاح" })
            : result.ToProblem();
    }
}
