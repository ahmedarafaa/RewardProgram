namespace RewardProgram.Application.Contracts.Auth.Additional;

public record RegistrationResponse
(
    string Message,
    int OtpExpiresInSeconds
);
