namespace RewardProgram.Application.Contracts.Redemption;

public record RedemptionListQuery(
    int Page = 1,
    int PageSize = 20
);
