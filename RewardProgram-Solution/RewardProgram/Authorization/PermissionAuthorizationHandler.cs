using Microsoft.AspNetCore.Authorization;
using RewardProgram.Domain.Constants;

namespace RewardProgram.API.Authorization;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the caller is a SystemAdmin
/// (full bypass) or holds the matching permission claim.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.IsInRole(UserRoles.SystemAdmin)
            || context.User.HasClaim(AdminPermissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
