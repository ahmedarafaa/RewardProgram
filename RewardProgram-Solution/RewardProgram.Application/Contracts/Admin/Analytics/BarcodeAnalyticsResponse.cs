namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record BarcodeAnalyticsResponse(
    int TotalGenerated,
    int TotalAvailable,
    int TotalSellerScanned,
    int TotalTechnicianScanned,
    int TotalConsumed,
    decimal ScanRate,
    List<ProductBarcodeItem> TopProductsByBarcodes
);

public record ProductBarcodeItem(
    string ProductId,
    string ProductName,
    string ProductCode,
    int TotalBarcodes,
    int ScannedCount,
    int ConsumedCount
);
