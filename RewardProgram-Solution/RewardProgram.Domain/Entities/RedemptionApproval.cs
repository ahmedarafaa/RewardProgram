using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Domain.Entities;

public class RedemptionApproval : TrackableEntity
{
    public string RedemptionRequestId { get; set; } = string.Empty;
    public string ApproverId { get; set; } = string.Empty;
    public ApprovalAction Action { get; set; }
    public string? RejectionReason { get; set; }
    public RedemptionRequestStatus FromStatus { get; set; }
    public RedemptionRequestStatus ToStatus { get; set; }

    // Navigation
    public RedemptionRequest RedemptionRequest { get; set; } = null!;
    public ApplicationUser Approver { get; set; } = null!;
}
