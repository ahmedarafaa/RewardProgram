using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption;

public record PendingRedemptionResponse(
    string Id,
    string UserFullName,
    string UserMobile,
    RedemptionMethod Method,
    RedemptionRequestStatus Status,
    decimal PointsAmount,
    decimal SarAmount,
    string? Iban,
    string? BankName,
    string? AccountHolderName,
    DateTime CreatedAt
);
