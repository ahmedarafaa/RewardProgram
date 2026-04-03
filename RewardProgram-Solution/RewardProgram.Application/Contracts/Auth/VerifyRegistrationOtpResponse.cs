namespace RewardProgram.Application.Contracts.Auth;

public record VerifyRegistrationOtpResponse(
    string VerificationToken,
    string MaskedMobileNumber
);
