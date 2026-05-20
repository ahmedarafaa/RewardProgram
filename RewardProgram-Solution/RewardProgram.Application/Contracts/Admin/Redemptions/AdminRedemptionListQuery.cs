using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Redemptions;

public record AdminRedemptionListQuery(
    string? UserId,
    RedemptionMethod? Method,
    RedemptionRequestStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    string? RegionId = null,
    int Page = 1,
    int PageSize = 20
);
