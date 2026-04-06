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
    string? AccountNumber,
    string? Address,
    string? SwiftCode,
    string? AccountName,
    DateTime CreatedAt
);
