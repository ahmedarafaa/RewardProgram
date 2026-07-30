namespace RewardProgram.Application.Contracts.Scan;

public record ScanBarcodeResponse(
    string ProductName,
    // English product name — null when the catalogue has no English entry, in which
    // case an English-language client falls back to ProductName.
    string? ProductNameEn,
    decimal PointsAwarded,
    decimal NewBalance,
    string BarcodeId,
    string BarcodeCode,
    string? Message = null
);
