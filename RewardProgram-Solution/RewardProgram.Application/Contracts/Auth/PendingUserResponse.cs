using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth;

public record PendingUserResponse(
    // User basics
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    RegistrationStatus RegistrationStatus,
    DateTime RegisteredAt,

    // ShopOwner profile
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShopImageUrl,
    string? ShopCode,

    // Location (resolved names)
    string? RegionName,
    string? CityName,
    string? DistrictName,
    string? Street,
    int? BuildingNumber,
    string? PostalCode,
    int? SubNumber,

    // Seller info
    string? ShopOwnerName,

    // Assignment info
    string? AssignedSalesManName
);
