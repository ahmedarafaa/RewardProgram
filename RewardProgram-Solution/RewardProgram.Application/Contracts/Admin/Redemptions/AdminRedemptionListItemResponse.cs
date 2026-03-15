using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Redemptions;

public record AdminRedemptionListItemResponse(
    string Id,
    string UserFullName,
    string UserMobile,
    RedemptionMethod Method,
    RedemptionRequestStatus Status,
    decimal PointsAmount,
    decimal SarAmount,
    DateTime CreatedAt
);
