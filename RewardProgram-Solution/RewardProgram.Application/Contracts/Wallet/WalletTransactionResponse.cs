using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Wallet;

public record WalletTransactionResponse(
    string Id,
    decimal Amount,
    decimal SarRate,
    decimal SarAmount,
    WalletTransactionType Type,
    string? Description,
    DateTime CreatedAt
);
