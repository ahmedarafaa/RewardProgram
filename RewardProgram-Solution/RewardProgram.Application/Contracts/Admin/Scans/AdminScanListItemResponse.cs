using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Scans;

public record AdminScanListItemResponse(
    string Id,
    string BarcodeCode,
    string ProductName,
    string ProductCode,
    int ProductPointValue,
    decimal PointsAwarded,
    ScannerRole ScannerRole,
    BarcodeStatus BarcodeStatus,
    string UserName,
    string UserMobile,
    DateTime ScannedAt
);
