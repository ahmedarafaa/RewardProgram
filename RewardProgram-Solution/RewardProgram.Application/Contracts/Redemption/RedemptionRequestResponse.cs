using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption;

public record RedemptionRequestResponse(
    string Id,
    RedemptionMethod Method,
    RedemptionRequestStatus Status,
    decimal PointsAmount,
    decimal SarRate,
    decimal SarAmount,
    string? Iban,
    string? BankName,
    string? AccountHolderName,
    DateTime? CashOtpExpiresAt,
    string? RejectionReason,
    DateTime CreatedAt
);
