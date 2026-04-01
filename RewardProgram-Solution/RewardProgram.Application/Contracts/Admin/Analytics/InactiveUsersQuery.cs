namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record InactiveUsersQuery(
    int InactiveDays = 30,
    int Page = 1,
    int PageSize = 20
);
