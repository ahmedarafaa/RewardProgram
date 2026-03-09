namespace RewardProgram.Application.Contracts.Scan;

public record ScanBarcodeResponse(
    string ProductName,
    decimal PointsAwarded,
    decimal NewBalance,
    string Message
);
