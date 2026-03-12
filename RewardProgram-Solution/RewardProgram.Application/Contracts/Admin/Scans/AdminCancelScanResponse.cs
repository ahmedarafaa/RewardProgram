namespace RewardProgram.Application.Contracts.Admin.Scans;

public record AdminCancelScanResponse(
    string ScanId,
    string BarcodeCode,
    decimal PointsReversed,
    decimal SarReversed,
    string Message
);
