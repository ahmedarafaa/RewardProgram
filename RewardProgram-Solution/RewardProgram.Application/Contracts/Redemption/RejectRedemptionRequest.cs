namespace RewardProgram.Application.Contracts.Redemption;

public record RejectRedemptionRequest(
    string RedemptionRequestId,
    string RejectionReason
);
