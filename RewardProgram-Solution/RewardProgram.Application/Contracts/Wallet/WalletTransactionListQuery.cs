using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Wallet;

public record WalletTransactionListQuery(
    WalletTransactionType? Type,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20
);
