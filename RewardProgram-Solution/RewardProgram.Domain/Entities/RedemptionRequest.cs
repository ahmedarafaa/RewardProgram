using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class RedemptionRequest : TrackableEntity
{
    public string UserId { get; set; } = string.Empty;
    public RedemptionMethod Method { get; set; }
    public RedemptionRequestStatus Status { get; set; }
    public decimal PointsAmount { get; set; }
    public decimal SarRate { get; set; }
    public decimal SarAmount { get; set; }

    // Bank transfer fields
    public string? Iban { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }

    // Cash handover fields
    public string? CashOtpHash { get; set; }
    public DateTime? CashOtpExpiresAt { get; set; }
    public int CashOtpAttempts { get; set; }
    public string? CashHandoverById { get; set; }
    public DateTime? CashHandoverAt { get; set; }

    // Rejection
    public string? RejectionReason { get; set; }
    public string? RejectedById { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser? CashHandoverBy { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public List<RedemptionApproval> Approvals { get; set; } = [];
}
