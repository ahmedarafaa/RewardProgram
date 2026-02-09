namespace RewardProgram.Application.Contracts.Auth;

public record VerifyOtpRequest(
    string PinId,
    string Otp
);