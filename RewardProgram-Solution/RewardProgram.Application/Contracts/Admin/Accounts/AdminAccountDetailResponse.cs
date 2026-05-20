namespace RewardProgram.Application.Contracts.Admin.Accounts;

/// <summary>Full detail of one admin-dashboard account, including granted permissions.</summary>
public record AdminAccountDetailResponse(
    string Id,
    string Username,
    string Name,
    bool IsSystemAdmin,
    bool IsDisabled,
    DateTime CreatedAt,
    IReadOnlyList<string> Permissions
);
