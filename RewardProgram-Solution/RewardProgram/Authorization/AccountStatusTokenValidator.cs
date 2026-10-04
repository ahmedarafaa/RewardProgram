using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using RewardProgram.Application.Helpers;
using RewardProgram.Infrastructure.Persistance;

namespace RewardProgram.API.Authorization;

/// <summary>
/// JwtBearer <c>OnTokenValidated</c> hook: rejects a correctly signed, unexpired
/// access token whose account has since been disabled or deleted. Without it a
/// switched-off SM/ZM/admin keeps working until the token expires (up to 60 min),
/// because disable/delete only revoke refresh tokens.
/// </summary>
public static class AccountStatusTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            context.Fail("Token has no subject.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();

        if (!cache.TryGetValue(AccountStatusCache.Key(userId), out bool isActive))
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            isActive = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => !u.IsDisabled && !u.IsAccountDeleted)
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            cache.Set(AccountStatusCache.Key(userId), isActive, AccountStatusCache.Ttl);
        }

        if (!isActive)
            context.Fail("Account is disabled or deleted.");
    }
}
