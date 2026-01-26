namespace RewardProgram.Application.Contracts.Auth;

public record RefreshTokenResponse
(
    string Token,
    string RefreshToken
);
