using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminAddUserResponse(
    string UserId,
    string Name,
    string MobileNumber,
    UserType UserType,
    string Message
);
