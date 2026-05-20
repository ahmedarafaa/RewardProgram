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
    string? AccountNumber,
    string? Address,
    string? SwiftCode,
    string? AccountName,
    DateTime? CashOtpExpiresAt,
    // Cash-handover OTP, decrypted for the owning user. Populated only while the
    // cash redemption is in-flight (not completed/rejected/cancelled) and the OTP
    // has not expired; null otherwise. See RedemptionService.GetVisibleCashOtp.
    string? CashOtp,
    string? RejectionReason,
    DateTime CreatedAt
);
