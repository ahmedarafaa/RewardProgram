namespace RewardProgram.Application.Contracts.Redemption;

public record ConfirmCashHandoverRequest(
    string RedemptionRequestId,
    string Otp
);
