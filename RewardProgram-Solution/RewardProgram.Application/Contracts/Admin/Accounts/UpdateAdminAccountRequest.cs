namespace RewardProgram.Application.Contracts.Admin.Accounts;

/// <summary>
/// Updates an admin account. <see cref="NewPassword"/> is optional — when null or
/// empty the existing password is kept.
/// </summary>
public record UpdateAdminAccountRequest(
    string Name,
    bool IsDisabled,
    string? NewPassword
);
