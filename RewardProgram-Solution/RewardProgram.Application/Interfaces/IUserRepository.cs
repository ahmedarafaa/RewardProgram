using Microsoft.AspNetCore.Identity;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Interfaces;

public interface IUserRepository
{
    Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct = default);
    Task<ApplicationUser?> FindByMobileAsync(string mobileNumber, CancellationToken ct = default);
    Task<ApplicationUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    /// <summary>
    /// Returns true when registering this mobile should be blocked. Blocked = a
    /// non-Rejected user already exists for the number. Rejected users are NOT
    /// blocked — re-registration is allowed and the prior record gets archived
    /// (tombstoned) inside the Register* transaction via
    /// <see cref="ArchiveRejectedUserByMobileAsync"/>.
    /// </summary>
    Task<bool> IsMobileBlockedAsync(string mobileNumber, CancellationToken ct = default);

    /// <summary>
    /// If a Rejected user exists with this mobile, tombstones the record so the
    /// number's unique indexes are freed for the new registration. Renames
    /// MobileNumber/UserName/PhoneNumber with a `DEL_&lt;ticks&gt;_` prefix,
    /// flags IsAccountDeleted + IsDisabled, clears FcmToken, revokes refresh
    /// tokens. No-op if no Rejected user exists for that mobile. Designed to
    /// run inside the same transaction as the new user's INSERT — if creation
    /// fails the rollback also unwinds the archive.
    /// </summary>
    Task ArchiveRejectedUserByMobileAsync(string mobileNumber, CancellationToken ct = default);
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
