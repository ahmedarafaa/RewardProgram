using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth;

public record PendingUserResponse
(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    RegistrationStatus RegistrationStatus,
    DateTime RegisteredAt,
    NationalAddressResponse? NationalAddress,

    // ShopOwner specific
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShopImageUrl,

    //Seller specific,
    string? ShopOwnerName,
    string? ShopCode
);
