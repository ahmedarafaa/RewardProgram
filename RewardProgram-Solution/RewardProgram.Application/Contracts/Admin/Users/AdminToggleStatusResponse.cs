namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminToggleStatusResponse(
    string UserId,
    bool IsDisabled,
    string Message
);
