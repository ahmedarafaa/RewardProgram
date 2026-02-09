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

    // National Address (resolved names)
    string? CityName,
    string? DistrictName,
    Zone? Zone,
    string? Street,
    int? BuildingNumber,
    string? PostalCode,
    int? SubNumber,

    // Assignment info
    string? AssignedSalesManName
);
