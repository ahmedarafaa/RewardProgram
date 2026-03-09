using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Wallet;

public record WalletTransactionListQuery(
    WalletTransactionType? Type,
    int Page = 1,
    int PageSize = 20
);
