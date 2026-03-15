using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption;

public record CreateRedemptionRequest(
    RedemptionMethod Method,
    decimal PointsAmount,
    string? Iban,
    string? BankName,
    string? AccountHolderName
);
