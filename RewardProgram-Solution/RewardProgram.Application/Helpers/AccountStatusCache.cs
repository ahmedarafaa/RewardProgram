using Microsoft.Extensions.Caching.Memory;

namespace RewardProgram.Application.Helpers;

/// <summary>
/// Short-lived cache of "is this account still active" used by the JWT validation
/// hook, so a disabled or deleted user's unexpired access token stops working
/// without a database round-trip on every request. Code that disables or deletes
/// an account calls <see cref="Invalidate"/> so the cut-off is immediate rather
/// than waiting out <see cref="Ttl"/>.
/// </summary>
public static class AccountStatusCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public static string Key(string userId) => $"account-active:{userId}";

    public static void Invalidate(IMemoryCache cache, string userId) => cache.Remove(Key(userId));
}
