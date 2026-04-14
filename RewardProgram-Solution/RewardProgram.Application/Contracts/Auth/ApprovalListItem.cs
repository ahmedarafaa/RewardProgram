using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth;

public enum ApprovalListStatusFilter
{
    All = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum ApprovalReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public record ApprovalListItem(
    string UserId,
    string Name,
    string MobileNumber,
    UserType UserType,
    string? StoreName,
    ApprovalReviewStatus ReviewStatus,
    RegistrationStatus CurrentStatus,
    DateTime ActivityAt,
    string? RejectionReason
);
