using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminUserListItemResponse(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    RegistrationStatus RegistrationStatus,
    bool IsDisabled,
    DateTime CreatedAt,
    string? RegionName,
    string? CityName,
    string? CustomerCode,
    string? StoreName
);
