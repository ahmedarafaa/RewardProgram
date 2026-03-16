namespace RewardProgram.Application.Contracts.Invitation;

public record InvitationInfoResponse(
    string InvitationCode,
    string ShareLink,
    string QrCodeBase64,
    int TotalInvitations,
    int ApprovedInvitations,
    decimal TotalPointsEarned
);
