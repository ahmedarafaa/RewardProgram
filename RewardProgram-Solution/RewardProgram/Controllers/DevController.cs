using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IApplicationDbContext _context;
    private readonly IHostEnvironment _environment;

    public DevController(
        IUserRepository userRepository,
        IApplicationDbContext context,
        IHostEnvironment environment)
    {
        _userRepository = userRepository;
        _context = context;
        _environment = environment;
    }

    [HttpGet("seeded-users")]
    public async Task<IActionResult> GetSeededUsers(CancellationToken ct)
    {
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
            return NotFound();

        var users = await _userRepository.Query()
            .Where(u => !u.IsDisabled)
            .OrderBy(u => u.UserType)
            .ThenBy(u => u.Name)
            .Select(u => new
            {
                u.Name,
                u.MobileNumber,
                Role = u.UserType.ToString(),
                Status = u.RegistrationStatus.ToString(),
                ManagedRegion = u.UserType == UserType.ZoneManager
                    ? _context.Regions
                        .Where(r => r.ZoneManagerId == u.Id)
                        .Select(r => r.NameAr)
                        .FirstOrDefault()
                    : null,
                ManagedCities = u.UserType == UserType.SalesMan
                    ? _context.Cities
                        .Where(c => c.ApprovalSalesManId == u.Id)
                        .Select(c => c.NameAr)
                        .ToList()
                    : null
            })
            .ToListAsync(ct);

        return Ok(users);
    }
}
