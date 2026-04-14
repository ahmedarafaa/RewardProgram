namespace RewardProgram.Application.Contracts.Auth;

public record RegisterResponse(
    string UserId,
    string Message,
    decimal? InvitationBonusPoints = null
);
