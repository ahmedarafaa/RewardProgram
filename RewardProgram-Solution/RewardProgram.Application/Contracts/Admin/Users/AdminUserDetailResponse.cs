using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminUserDetailResponse(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    RegistrationStatus RegistrationStatus,
    bool IsDisabled,
    bool IsAccountDeleted,
    DateTime? AccountDeletedAt,
    string? DeletionSource,
    DateTime? RestoredAt,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles,

    // Home address (ShopOwner / Seller / Technician — null for staff)
    AdminUserAddress? Address,

    // Shop info (ShopOwner / Seller — null otherwise; pulls from ShopData by CustomerCode)
    AdminUserShopInfo? Shop,

    // SalesMan ownership (empty for non-SalesMan)
    IReadOnlyList<NamedRef> OwnedCities,

    // ZoneManager ownership (null for non-ZoneManager or idle ZM)
    NamedRef? ManagedRegion,

    // Invitation
    string? InvitationCode,
    string? InvitedByUserId,
    int InviterRewardCount
);

public record AdminUserAddress(
    string CityId,
    string CityName,
    string CityNameEn,
    string RegionId,
    string RegionName,
    string RegionNameEn,
    string Street,
    int BuildingNumber,
    string PostalCode,
    int SubNumber,
    string District
);

public record AdminUserShopInfo(
    string CustomerCode,
    string StoreName,
    string Vat,
    string Crn,
    string ShortAddress,
    string ShopImageUrl,
    // Shop's own address (separate from user's home address — they can differ)
    string? ShopCityId,
    string? ShopCityName,
    string? ShopCityNameEn,
    string? ShopStreet,
    int? ShopBuildingNumber,
    string? ShopPostalCode,
    int? ShopSubNumber,
    string? ShopDistrict
);
