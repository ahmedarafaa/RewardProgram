using Microsoft.AspNetCore.Identity;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Interfaces;

public interface IUserRepository
{
    Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct = default);
    Task<ApplicationUser?> FindByMobileAsync(string mobileNumber, CancellationToken ct = default);
    Task<ApplicationUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> MobileExistsAsync(string mobileNumber, CancellationToken ct = default);
    IQueryable<ApplicationUser> Query();
    Task<IdentityResult> CreateAsync(ApplicationUser user);
    Task<IdentityResult> UpdateAsync(ApplicationUser user);
    Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role);
    Task<IList<string>> GetRolesAsync(ApplicationUser user);
}
