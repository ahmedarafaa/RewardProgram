using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record ValidateRegistrationFieldsRequest(
    string VerificationToken,
    UserType UserType,
    string? InvitationCode = null,
    string? CustomerCode = null,
    string? CityId = null,
    string? VAT = null,
    string? CRN = null,
    string? ShortAddress = null
);

public record ValidationFieldError(string Field, string Code, string Message);

public record ValidateRegistrationFieldsResponse(
    bool IsValid,
    IReadOnlyList<ValidationFieldError> Errors
);
