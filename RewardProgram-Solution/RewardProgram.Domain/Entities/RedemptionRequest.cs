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
    public string? AccountNumber { get; set; }
    public string? Address { get; set; }
    public string? SwiftCode { get; set; }
    public string? AccountName { get; set; }

    // Cash handover fields
    public string? CashOtpHash { get; set; }
    // Encrypted (reversible) copy of the cash OTP, kept only so it can be shown
    // back to the owning user in their wallet history. Verification still uses
    // CashOtpHash — this column is display-only and may be null if the Data
    // Protection key ring is unavailable.
    public string? CashOtpEncrypted { get; set; }
    public DateTime? CashOtpExpiresAt { get; set; }
    public int CashOtpAttempts { get; set; }
    public string? CashHandoverById { get; set; }
    public DateTime? CashHandoverAt { get; set; }

    // Rejection
    public string? RejectionReason { get; set; }
    public string? RejectedById { get; set; }

    // Concurrency control
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser? CashHandoverBy { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public List<RedemptionApproval> Approvals { get; set; } = [];
}
