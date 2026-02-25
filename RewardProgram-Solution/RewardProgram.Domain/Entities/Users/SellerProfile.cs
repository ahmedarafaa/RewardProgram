namespace RewardProgram.Domain.Entities.Users;

public class SellerProfile : UserProfile
{
    public string CustomerCode { get; set; } = string.Empty;  // FK → ErpCustomer.CustomerCode

    // Navigation
    public ErpCustomer ErpCustomer { get; set; } = null!;
}
