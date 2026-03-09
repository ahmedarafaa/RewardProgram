using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class ScanRecord : TrackableEntity
{
    public string BarcodeId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ScannerRole ScannerRole { get; set; }
    public decimal PointsAwarded { get; set; }

    // Navigation
    public ProductBarcode Barcode { get; set; } = null!;
    public Users.ApplicationUser User { get; set; } = null!;
}
