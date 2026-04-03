namespace RewardProgram.Application.Contracts.Auth;

public record VerifyRegistrationOtpRequest(
    string PinId,
    string Otp,
    string MobileNumber
);
