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
    // "Self" when the user closed their own account via the public delete-account
    // endpoint, "Admin" when an admin soft-deleted them, null when not deleted.
    // The frontend uses this to gate the Restore confirmation dialog: restoring
    // a self-deleted account is a sensitive override and should be confirmed.
    string? DeletionSource,
    DateTime? RestoredAt,
    DateTime CreatedAt,
    string? RegionName,
    string? CityName,
    string? CustomerCode,
    string? StoreName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<NamedRef> OwnedCities,
    NamedRef? ManagedRegion
);

public record NamedRef(string Id, string NameAr, string NameEn);
