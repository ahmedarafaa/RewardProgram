namespace RewardProgram.Application.Contracts.Admin.Accounts;

/// <summary>Creates a new Admin-role dashboard account. It starts with no permissions.</summary>
public record CreateAdminAccountRequest(
    string Username,
    string Name,
    string Password
);
