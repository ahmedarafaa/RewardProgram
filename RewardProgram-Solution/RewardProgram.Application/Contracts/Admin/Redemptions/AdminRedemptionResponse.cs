using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Redemptions;

public record AdminRedemptionResponse(
    string Id,
    string UserId,
    string UserFullName,
    string UserMobile,
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
    string? CashHandoverByName,
    DateTime? CashHandoverAt,
    string? RejectionReason,
    string? RejectedByName,
    DateTime CreatedAt,
    List<AdminRedemptionApprovalResponse> Approvals
);
