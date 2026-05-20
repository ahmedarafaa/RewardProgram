namespace RewardProgram.Application.Contracts.Admin.Accounts;

/// <summary>Replaces an admin account's full permission set with the supplied list.</summary>
public record SetAdminPermissionsRequest(
    List<string> Permissions
);
