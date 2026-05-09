namespace RewardProgram.Application.Contracts.Invitation;

public record InvitationInfoResponse(
    string InvitationCode,
    string ShareLink,
    int TotalInvitations,
    int ApprovedInvitations,
    decimal TotalPointsEarned
);
