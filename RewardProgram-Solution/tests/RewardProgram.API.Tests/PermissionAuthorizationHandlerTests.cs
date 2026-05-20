using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using Xunit;

namespace RewardProgram.API.Tests;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext Context(PermissionRequirement requirement, ClaimsPrincipal user)
        => new([requirement], user, resource: null);

    private static ClaimsPrincipal UserWith(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "test"));

    [Fact]
    public async Task SystemAdmin_ShouldSatisfyAnyPermission()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(AdminPermissions.UsersManage);
        var ctx = Context(requirement, UserWith(new Claim(ClaimTypes.Role, UserRoles.SystemAdmin)));

        await handler.HandleAsync(ctx);

        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_WithMatchingPermissionClaim_ShouldSucceed()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(AdminPermissions.UsersView);
        var ctx = Context(requirement, UserWith(
            new Claim(ClaimTypes.Role, UserRoles.Admin),
            new Claim(AdminPermissions.ClaimType, AdminPermissions.UsersView)));

        await handler.HandleAsync(ctx);

        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_WithoutTheRequiredPermission_ShouldNotSucceed()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(AdminPermissions.UsersManage);
        var ctx = Context(requirement, UserWith(
            new Claim(ClaimTypes.Role, UserRoles.Admin),
            new Claim(AdminPermissions.ClaimType, AdminPermissions.UsersView)));

        await handler.HandleAsync(ctx);

        ctx.HasSucceeded.Should().BeFalse();
    }
}
