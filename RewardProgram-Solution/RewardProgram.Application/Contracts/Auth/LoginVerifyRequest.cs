namespace RewardProgram.Application.Contracts.Auth;

public record LoginVerifyRequest(
    string PinId,
    string Otp
);
