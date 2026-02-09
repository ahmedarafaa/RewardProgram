namespace RewardProgram.Application.Contracts.Auth;

public record OtpVerificationResult(
    string MobileNumber,
    string? RegistrationData
);
