using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth;

public enum ApprovalListStatusFilter
{
    All = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum ApprovalReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public record ApprovalListItem(
    // User basics
    string UserId,
    string Name,
    string MobileNumber,
    UserType UserType,

    // ERP customer
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

    // Assignment / referral
    string? AssignedSalesManName,
    string? InvitedByName,

    // Review state (list-specific)
    ApprovalReviewStatus ReviewStatus,
    RegistrationStatus CurrentStatus,
    DateTime ActivityAt,
    string? RejectionReason
);
