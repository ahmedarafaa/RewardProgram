using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Persistance;

public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct = default)
        => await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task<ApplicationUser?> FindByMobileAsync(string mobileNumber, CancellationToken ct = default)
        => await _userManager.Users.FirstOrDefaultAsync(u => u.MobileNumber == mobileNumber, ct);

    public async Task<ApplicationUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await _userManager.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken), ct);

    public async Task<bool> MobileExistsAsync(string mobileNumber, CancellationToken ct = default)
        => await _userManager.Users.AnyAsync(u => u.MobileNumber == mobileNumber, ct);

    public IQueryable<ApplicationUser> Query() => _userManager.Users;

    public Task<IdentityResult> CreateAsync(ApplicationUser user) => _userManager.CreateAsync(user);

    public Task<IdentityResult> UpdateAsync(ApplicationUser user) => _userManager.UpdateAsync(user);

    public Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role) => _userManager.AddToRoleAsync(user, role);

    public Task<IList<string>> GetRolesAsync(ApplicationUser user) => _userManager.GetRolesAsync(user);
}
