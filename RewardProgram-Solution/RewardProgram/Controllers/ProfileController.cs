using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Profile;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(Roles = $"{UserRoles.Seller},{UserRoles.Technician},{UserRoles.ShopOwner},{UserRoles.SalesMan},{UserRoles.ZoneManager}")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public ProfileController(IProfileService profileService, IStringLocalizer<ErrorMessages> localizer)
    {
        _profileService = profileService;
        _localizer = localizer;
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
    [Authorize(Roles = $"{UserRoles.Seller},{UserRoles.Technician},{UserRoles.ShopOwner}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _profileService.DeleteAccountAsync(userId, ct);
        return result.IsSuccess
            ? Ok(new { message = _localizer["Profile.AccountDeleted"].Value })
            : result.ToProblem();
    }
}
