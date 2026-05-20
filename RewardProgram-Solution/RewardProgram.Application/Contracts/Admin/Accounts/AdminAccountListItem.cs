namespace RewardProgram.Application.Contracts.Admin.Accounts;

/// <summary>One admin-dashboard account as shown in the management list.</summary>
public record AdminAccountListItem(
    string Id,
    string Username,
    string Name,
    bool IsSystemAdmin,
    bool IsDisabled,
    int PermissionCount,
    DateTime CreatedAt
);
