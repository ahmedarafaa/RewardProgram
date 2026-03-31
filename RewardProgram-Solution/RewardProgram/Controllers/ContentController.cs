using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts.Admin.Content;
using RewardProgram.Application.Interfaces.Admin;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/content")]
public class ContentController : ControllerBase
{
    private readonly IAdminContentService _contentService;

    public ContentController(IAdminContentService contentService)
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

    [HttpGet("about-app")]
    [ProducesResponseType(typeof(AboutAppResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAboutApp(CancellationToken ct)
    {
        var result = await _contentService.GetAboutAppAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
