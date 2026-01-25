using RewardProgram.Application.Contracts.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth.UsersDTO;

public record RegisterSellerRequest
(
     string Name,
     string MobileNumber ,
     string ShopCode, // To link with ShopOwner
     NationalAddressResponse NationalAddress
);

