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
    Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName);

    /// <summary>
    /// Bulk-revokes every active refresh token for the given user. Returns the
    /// number of tokens revoked. Use when disabling/deleting a user so that any
    /// in-flight refresh sessions are killed immediately, without needing to
    /// load the (potentially large) RefreshTokens navigation.
    /// </summary>
    Task<int> RevokeAllRefreshTokensAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Bumps the user's Identity SecurityStamp. Existing access tokens that embedded
    /// the prior stamp will be rejected at the next validation tick. Use after
    /// disabling/deleting a user, or after any other security-sensitive change.
    /// </summary>
    Task<IdentityResult> UpdateSecurityStampAsync(ApplicationUser user);
}
