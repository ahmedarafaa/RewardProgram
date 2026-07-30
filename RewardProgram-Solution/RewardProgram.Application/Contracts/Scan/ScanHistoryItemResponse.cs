using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Scan;

public record ScanHistoryItemResponse(
    string Id,
    string BarcodeCode,
    string ProductName,
    string? ProductNameEn,
    string ProductCode,
    int ProductPointValue,
    decimal PointsAwarded,
    ScannerRole ScannerRole,
    BarcodeStatus BarcodeStatus,
    DateTime ScannedAt,
    double? Latitude,
    double? Longitude
);
