using Microsoft.AspNetCore.Authorization;

namespace RewardProgram.API.Authorization;

/// <summary>Authorization requirement for a single admin-dashboard permission.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
