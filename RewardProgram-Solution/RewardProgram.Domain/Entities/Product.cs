namespace RewardProgram.Domain.Entities;

public class Product : TrackableEntity
{
    public string Name { get; set; } = string.Empty;

    // English product name from the ERP catalogue. Nullable because a handful of
    // codes have no English entry yet — surfaces that show English fall back to Name.
    public string? NameEn { get; set; }

    public string ProductCode { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }

    // Navigation
    public List<ProductBarcode> Barcodes { get; set; } = [];
}
