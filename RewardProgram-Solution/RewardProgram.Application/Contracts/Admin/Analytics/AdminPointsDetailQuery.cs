using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record AdminPointsDetailQuery(
    string? UserId,
    string? RegionId,
    DateTime? DateFrom,
    DateTime? DateTo,
    WalletTransactionType? Type,
    int Page = 1,
    int PageSize = 20
);
