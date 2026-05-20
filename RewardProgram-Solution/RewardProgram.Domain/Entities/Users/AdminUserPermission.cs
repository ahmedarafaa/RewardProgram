namespace RewardProgram.Domain.Entities.Users;

/// <summary>
/// Grants one admin-dashboard permission to one <c>Admin</c>-role user.
/// SystemAdmin users have no rows here — they bypass permission checks.
/// The <c>Permission</c> value is one of <c>AdminPermissions</c>.
/// </summary>
public class AdminUserPermission
{
    public string Id { get; set; } = Guid.CreateVersion7().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;
}
