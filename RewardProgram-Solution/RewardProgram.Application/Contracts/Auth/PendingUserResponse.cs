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

    // ERP Customer
    string? CustomerCode,
    string? CustomerName,

    // Shop data (from ShopData entity)
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShortAddress,
    string? ShopImageUrl,

    // Location (resolved names)
    string? RegionName,
    string? CityName,
    string? Street,
    int? BuildingNumber,
    string? PostalCode,
    int? SubNumber,
    string? District,

    // Assignment info
    string? AssignedSalesManName
);
