using Microsoft.AspNetCore.Authorization;

namespace RewardProgram.API.Authorization;

/// <summary>
/// Restricts an admin endpoint to callers holding the given permission. A
/// <c>SystemAdmin</c> bypasses the check; an <c>Admin</c> needs the matching
/// permission claim (see <see cref="PermissionAuthorizationHandler"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    /// <summary>Prefix for the per-permission authorization policy names.</summary>
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission) => Policy = PolicyPrefix + permission;
}
