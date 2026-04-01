using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record AdminPointsDetailItemResponse(
    string TransactionId,
    string UserId,
    string UserName,
    string UserMobile,
    decimal Amount,
    decimal SarAmount,
    WalletTransactionType Type,
    string? Description,
    DateTime CreatedAt
);
