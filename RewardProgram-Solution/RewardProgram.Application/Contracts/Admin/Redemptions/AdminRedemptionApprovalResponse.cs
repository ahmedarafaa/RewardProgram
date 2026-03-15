using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Redemptions;

public record AdminRedemptionApprovalResponse(
    string Id,
    string ApproverName,
    ApprovalAction Action,
    string? RejectionReason,
    RedemptionRequestStatus FromStatus,
    RedemptionRequestStatus ToStatus,
    DateTime CreatedAt
);
