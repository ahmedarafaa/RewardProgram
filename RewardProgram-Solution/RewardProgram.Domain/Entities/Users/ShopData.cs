namespace RewardProgram.Domain.Entities.Users;

public class ShopData : TrackableEntity
{
    public string CustomerCode { get; set; } = string.Empty;  // FK → ErpCustomer.CustomerCode (unique)
    public string StoreName { get; set; } = string.Empty;
    public string VAT { get; set; } = string.Empty;
    public string CRN { get; set; } = string.Empty;
    public string ShopImageUrl { get; set; } = string.Empty;

    // Flat address (no DistrictId)
    public string CityId { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public int BuildingNumber { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public int SubNumber { get; set; }

    // Audit — who filled it first
    public string EnteredByUserId { get; set; } = string.Empty;

    // Navigation properties
    public ErpCustomer ErpCustomer { get; set; } = null!;
    public ApplicationUser EnteredByUser { get; set; } = null!;
    public City City { get; set; } = null!;
}
