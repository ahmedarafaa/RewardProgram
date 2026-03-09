namespace RewardProgram.Application.Contracts.Scan;

public record ScanHistoryQuery(
    int Page = 1,
    int PageSize = 20
);
