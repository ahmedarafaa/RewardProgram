using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Helpers;

public static class UserCreationHelper
{
    /// <summary>
    /// Creates a user and assigns a role. Returns the caller-supplied failureError on any Identity failure.
    /// </summary>
    public static async Task<Result> CreateWithRoleAsync(
        IUserRepository userRepository,
        ApplicationUser user,
        string role,
        Error failureError,
        ILogger logger)
    {
        var createResult = await userRepository.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create {Role} user: {Errors}",
                role, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return Result.Failure(failureError);
        }

        var roleResult = await userRepository.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Failed to add {Role} role to user {UserId}: {Errors}",
                role, user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            return Result.Failure(failureError);
        }

        return Result.Success();
    }
}
