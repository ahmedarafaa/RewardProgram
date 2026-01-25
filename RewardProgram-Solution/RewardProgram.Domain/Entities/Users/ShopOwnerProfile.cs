using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.Users;

public class ShopOwnerProfile : UserProfile
{
    public string StoreName { get; set; } = string.Empty;
    public string VAT { get; set; } = string.Empty;
    public string CRN { get; set; } = string.Empty;
    public string? ShopCode { get; set; }
    public string ShopImageUrl { get; set; } = string.Empty;

    public List<SellerProfile> Sellers { get; set; } = new();
}
