using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class RoleErrors
{
    public static readonly Error RoleNotFound =
        new("Role.RoleNotFound", "Role is not found", 404);

    public static readonly Error InvalidPermissions =
        new("Role.InvalidPermissions", "Invalid permissions", 400);

    public static readonly Error DuplicatedRole =
        new("Role.DuplicatedRole", "Another role with the same name is already exists", 409);
}
