using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record AdminUserAnalyticsResponse(
    List<UserTypeCount> CountByUserType,
    List<RegistrationStatusCount> CountByRegistrationStatus,
    List<RegionUserCount> CountByRegion,
    List<MonthlyCount> RegistrationTrend
);

public record UserTypeCount(UserType UserType, int Count);
public record RegistrationStatusCount(RegistrationStatus Status, int Count);
public record RegionUserCount(string RegionId, string RegionNameAr, string RegionNameEn, int Count);
public record MonthlyCount(int Year, int Month, int Count);
