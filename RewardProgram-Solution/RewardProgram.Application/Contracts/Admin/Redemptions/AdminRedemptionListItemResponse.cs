using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Redemptions;

public record AdminRedemptionListItemResponse(
    string Id,
    string UserFullName,
    string UserMobile,
    // Customer (shop) the requester belongs to. Populated for Sellers via their
    // SellerProfile → ErpCustomer; null for Technicians (no customer code).
    string? CustomerCode,
    string? CustomerName,
    RedemptionMethod Method,
    RedemptionRequestStatus Status,
    decimal PointsAmount,
    decimal SarAmount,
    DateTime CreatedAt
);
