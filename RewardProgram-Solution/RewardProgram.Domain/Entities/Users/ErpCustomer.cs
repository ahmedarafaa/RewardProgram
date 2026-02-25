namespace RewardProgram.Domain.Entities.Users;

public class ErpCustomer : TrackableEntity
{
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;

    // Navigation properties
    public ShopData? ShopData { get; set; }
    public List<ShopOwnerProfile> ShopOwners { get; set; } = [];
    public List<SellerProfile> Sellers { get; set; } = [];
}
