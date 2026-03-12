namespace RewardProgram.Application.Contracts.Scan;

public record ScanBarcodeRequest(
    string BarcodeCode,
    double? Latitude = null,
    double? Longitude = null
);
