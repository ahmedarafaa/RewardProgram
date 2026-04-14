using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption;

public enum RedemptionListStatusFilter
{
    All = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum RedemptionReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public record RedemptionListItem(
    string RequestId,
    string UserId,
    string UserName,
    string UserMobile,
    RedemptionMethod Method,
    decimal PointsAmount,
    decimal SarAmount,
    RedemptionReviewStatus ReviewStatus,
    RedemptionRequestStatus CurrentStatus,
    DateTime ActivityAt,
    string? RejectionReason
);
