using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record TopPerformersResponse(
    List<TopPerformerItem> TopSellers,
    List<TopPerformerItem> TopTechnicians
);

public record TopPerformerItem(
    string UserId,
    string UserName,
    string MobileNumber,
    string? RegionNameAr,
    string? RegionNameEn,
    decimal TotalPointsEarned,
    int TotalScans
);
