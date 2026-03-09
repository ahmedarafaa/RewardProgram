namespace RewardProgram.Domain.Entities;

public class Product : TrackableEntity
{
    public string Name { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }

    // Navigation
    public List<ProductBarcode> Barcodes { get; set; } = [];
}
