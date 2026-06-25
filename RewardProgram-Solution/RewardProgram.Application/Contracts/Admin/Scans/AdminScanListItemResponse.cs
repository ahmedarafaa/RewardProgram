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
    // Customer (shop) the scanner belongs to. Populated for Sellers via their
    // SellerProfile → ErpCustomer; null for Technicians (no customer code).
    string? CustomerCode,
    string? CustomerName,
    DateTime ScannedAt,
    double? Latitude,
    double? Longitude
);
