namespace RewardProgram.Application.Contracts.Scan;

public record ScanHistoryQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20
);
