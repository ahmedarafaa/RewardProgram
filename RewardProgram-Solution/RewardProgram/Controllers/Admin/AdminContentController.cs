using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Admin.Content;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/content")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminContentController : ControllerBase
{
    private readonly IAdminContentService _contentService;

    public AdminContentController(IAdminContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet("contact-us")]
    [ProducesResponseType(typeof(ContactUsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContactUs(CancellationToken ct)
    {
        var result = await _contentService.GetContactUsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("contact-us")]
    [ProducesResponseType(typeof(ContactUsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateContactUs([FromBody] UpdateContactUsRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _contentService.UpdateContactUsAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("about-app")]
    [ProducesResponseType(typeof(AboutAppResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAboutApp(CancellationToken ct)
    {
        var result = await _contentService.GetAboutAppAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("about-app")]
    [ProducesResponseType(typeof(AboutAppResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAboutApp([FromBody] UpdateAboutAppRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _contentService.UpdateAboutAppAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
