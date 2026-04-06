using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption;

public record CreateRedemptionRequest(
    RedemptionMethod Method,
    decimal PointsAmount,
    string? Iban,
    string? AccountNumber,
    string? Address,
    string? SwiftCode,
    string? AccountName
);
