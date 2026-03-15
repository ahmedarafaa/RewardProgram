namespace RewardProgram.Application.Contracts.Redemption;

public record AvailableBalanceResponse(
    decimal TotalBalance,
    decimal HeldBalance,
    decimal AvailableBalance,
    decimal AvailableSarBalance
);
