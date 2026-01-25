using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.Users;

public class SellerProfile : UserProfile
{
    public string ShopOwnerId { get; set; } = string.Empty;  // FK to ShopOwnerProfile.UserId
    public ShopOwnerProfile ShopOwner { get; set; } = null!;
}
