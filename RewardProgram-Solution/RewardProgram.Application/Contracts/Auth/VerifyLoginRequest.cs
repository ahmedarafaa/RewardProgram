namespace RewardProgram.Application.Contracts.Auth;

public record VerifyLoginRequest
(
    string MobileNumber,
    string Otp
);
