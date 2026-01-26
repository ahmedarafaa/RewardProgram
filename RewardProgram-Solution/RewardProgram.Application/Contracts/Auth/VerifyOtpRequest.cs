namespace RewardProgram.Application.Contracts.Auth;

public record VerifyOtpRequest
(
    string MobileNumber,
    string Otp
);