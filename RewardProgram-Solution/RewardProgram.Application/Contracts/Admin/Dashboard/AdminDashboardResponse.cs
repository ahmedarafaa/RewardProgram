namespace RewardProgram.Application.Contracts.Admin.Dashboard;

public record AdminDashboardResponse(
    int TotalShopOwners,
    int TotalSellers,
    int TotalTechnicians,
    int TotalPendingApprovals,
    decimal TotalPointsEarned,
    decimal TotalPointsRedeemed,
    decimal TotalSarRedeemed,
    int TotalActiveBarcodes,
    int TotalScans,
    int PendingRedemptions,
    int TotalInvitations,
    int TotalNotificationsSent,
    int TotalDeletedAccounts
);
