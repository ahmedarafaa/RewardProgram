using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Dashboard;

public record DashboardResponse(
    string UserName,
    decimal Points,
    decimal SarBalance,
    decimal MinimumRedemptionPoints,
    decimal PointsToRedeem,
    bool CanRedeem,
    List<DashboardTransactionItem> RecentTransactions
);

public record DashboardTransactionItem(
    string Id,
    decimal Amount,
    decimal SarAmount,
    WalletTransactionType Type,
    string? Description,
    DateTime CreatedAt
);
