namespace RewardProgram.Domain.Enums;

public enum RedemptionRequestStatus
{
    PendingSalesMan = 1,
    PendingZoneManager = 2,
    PendingAdmin = 3,
    AdminApproved = 4,
    Completed = 5,
    Rejected = 6,
    Cancelled = 7
}
