using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminStaffRolesResponse(
    string UserId,
    string Name,
    // Primary type after the change. Sticky: unchanged while its role is still in the
    // set; switches to the remaining role only when the primary role was removed.
    UserType UserType,
    IReadOnlyList<string> Roles,
    bool IsDualRole,
    string Message
);
