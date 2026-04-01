using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record InactiveUserItem(
    string UserId,
    string UserName,
    string MobileNumber,
    UserType UserType,
    DateTime? LastScanDate,
    int DaysSinceLastScan
);
