using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class ProductBarcode : TrackableEntity
{
    public string Code { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public BarcodeStatus Status { get; set; } = BarcodeStatus.Available;
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Product Product { get; set; } = null!;
    public List<ScanRecord> ScanRecords { get; set; } = [];
}
