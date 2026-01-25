using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Interfaces.Auth;

namespace RewardProgram.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IAuthService authservice, ILogger<AuthController> logger) : ControllerBase
{
    private readonly IAuthService _authservice = authservice;
    private readonly ILogger<AuthController> _logger = logger;


}
