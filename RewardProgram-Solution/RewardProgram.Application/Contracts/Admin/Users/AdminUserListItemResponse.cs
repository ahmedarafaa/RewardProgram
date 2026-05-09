using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminUserListItemResponse(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    RegistrationStatus RegistrationStatus,
    bool IsDisabled,
    bool IsAccountDeleted,
    DateTime? AccountDeletedAt,
    DateTime CreatedAt,
    string? RegionName,
    string? CityName,
    string? CustomerCode,
    string? StoreName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<NamedRef> OwnedCities,
    NamedRef? ManagedRegion
);

public record NamedRef(string Id, string NameAr);
